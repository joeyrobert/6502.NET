/* Copyright (c) 2009 Joseph Robert. All rights reserved.
 *
 * This file is part of 6502.NET.
 *
 * 6502.NET is free software; you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as
 * published by the Free Software Foundation; either version 3.0  of
 * the License, or (at your option) any later version.
 *
 * 6502.NET is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with 6502.NET.  If not, see <http://www.gnu.org/licenses/>.
 */

namespace Emulator.Interpreter;

/// <summary>
/// MOS 6502 core. Implements all 151 documented opcodes with cycle counts, decimal mode
/// and the well-known NMOS quirks (JMP indirect page wrap, zero page wrap).
/// </summary>
public class Processor
{
    const int StackBase = 0x0100;
    public const int NmiVector = 0xFFFA;
    public const int ResetVector = 0xFFFC;
    public const int IrqVector = 0xFFFE;

    readonly Memory _memory;

    /// <summary>Slows execution down with a busy loop, for the interactive front ends.</summary>
    public bool LimitProcessorSpeed { get; set; }

    /// <summary>Iterations of the busy loop per instruction when <see cref="LimitProcessorSpeed"/> is set.</summary>
    public int SpinWait { get; set; } = 5000;

    // Registers
    public byte A { get; set; }
    public byte X { get; set; }
    public byte Y { get; set; }
    public byte SP { get; set; } = 0xFD;
    /// <summary>Address of the next instruction to be executed.</summary>
    public ushort PC { get; set; }

    // Flags
    public bool Carry { get; set; }
    public bool Zero { get; set; }
    public bool InterruptDisable { get; set; } = true;
    public bool Decimal { get; set; }
    /// <summary>Set once a BRK has been executed (informational; the real flag only exists on the stack).</summary>
    public bool Break { get; set; }
    public bool Overflow { get; set; }
    public bool Negative { get; set; }

    /// <summary>Total cycles executed so far.</summary>
    public long Cycles { get; private set; }

    /// <summary>Total instructions executed so far.</summary>
    public long Instructions { get; private set; }

    /// <summary>Halts <see cref="Step"/> when false. A BRK with a zero IRQ vector clears it.</summary>
    public bool ProgramRunning
    {
        get => _running;
        set => _running = value;
    }

    volatile bool _running = true;

    /// <summary>The processor status register as pushed by PHP/BRK (bit 5 set, bit 4 clear).</summary>
    public byte Status
    {
        get => (byte)(0x20 | (Carry ? 0x01 : 0) | (Zero ? 0x02 : 0) | (InterruptDisable ? 0x04 : 0)
            | (Decimal ? 0x08 : 0) | (Overflow ? 0x40 : 0) | (Negative ? 0x80 : 0));
        set
        {
            Carry = (value & 0x01) != 0;
            Zero = (value & 0x02) != 0;
            InterruptDisable = (value & 0x04) != 0;
            Decimal = (value & 0x08) != 0;
            Overflow = (value & 0x40) != 0;
            Negative = (value & 0x80) != 0;
        }
    }

    public Processor(Memory memory) => _memory = memory;

    /// <param name="memory">The computer's memory.</param>
    /// <param name="programCounter">First location to be executed.</param>
    public Processor(Memory memory, ushort programCounter) : this(memory) => PC = programCounter;

    /// <summary>Resets registers and loads the program counter from the reset vector.</summary>
    public void Reset()
    {
        A = X = Y = 0;
        SP = 0xFD;
        Status = 0x24;
        Break = false;
        PC = (ushort)_memory.ReadWord(ResetVector);
        ProgramRunning = true;
    }

    /// <summary>Maskable interrupt request. Ignored while the interrupt-disable flag is set.</summary>
    public void Irq()
    {
        if (InterruptDisable) return;
        Interrupt(IrqVector, breakFlag: false);
    }

    /// <summary>Non-maskable interrupt.</summary>
    public void Nmi() => Interrupt(NmiVector, breakFlag: false);

    void Interrupt(int vector, bool breakFlag)
    {
        PushWord(PC);
        Push((byte)(Status | (breakFlag ? 0x10 : 0)));
        InterruptDisable = true;
        PC = (ushort)_memory.ReadWord(vector);
        Cycles += 7;
    }

    /// <summary>Executes a single instruction (alias of <see cref="Step"/>).</summary>
    public void Execute() => Step();

    /// <summary>Runs until the program halts or <paramref name="maxInstructions"/> have executed.</summary>
    /// <returns>The number of instructions executed.</returns>
    public long Run(long maxInstructions = long.MaxValue)
    {
        long start = Instructions;
        while (ProgramRunning && Instructions - start < maxInstructions)
            Step();
        return Instructions - start;
    }

    // --- Memory and stack helpers ---

    byte Fetch() => _memory.Read(PC++);

    int FetchWord()
    {
        int lo = Fetch();
        return lo | (Fetch() << 8);
    }

    byte Read(int address) => _memory.Read(address & 0xFFFF);

    void Write(int address, byte value) => _memory.Set(address & 0xFFFF, value);

    void Push(byte value) => Write(StackBase + SP--, value);

    byte Pull() => Read(StackBase + ++SP);

    void PushWord(int value)
    {
        Push((byte)(value >> 8));
        Push((byte)value);
    }

    int PullWord()
    {
        int lo = Pull();
        return lo | (Pull() << 8);
    }

    void SetNZ(byte value)
    {
        Zero = value == 0;
        Negative = (value & 0x80) != 0;
    }

    /// <summary>Resolves the effective address of an operand, fetching operand bytes.</summary>
    int Address(AddressingMode mode, out bool pageCrossed)
    {
        pageCrossed = false;
        int address, baseAddress;
        switch (mode)
        {
            case AddressingMode.Immediate:
                return PC++;
            case AddressingMode.ZeroPage:
                return Fetch();
            case AddressingMode.ZeroPageX:
                return (Fetch() + X) & 0xFF;
            case AddressingMode.ZeroPageY:
                return (Fetch() + Y) & 0xFF;
            case AddressingMode.Absolute:
                return FetchWord();
            case AddressingMode.AbsoluteX:
                baseAddress = FetchWord();
                address = (baseAddress + X) & 0xFFFF;
                pageCrossed = (baseAddress & 0xFF00) != (address & 0xFF00);
                return address;
            case AddressingMode.AbsoluteY:
                baseAddress = FetchWord();
                address = (baseAddress + Y) & 0xFFFF;
                pageCrossed = (baseAddress & 0xFF00) != (address & 0xFF00);
                return address;
            case AddressingMode.Indirect: // JMP only; the pointer high byte never carries into the next page
                int pointer = FetchWord();
                return Read(pointer) | (Read((pointer & 0xFF00) | ((pointer + 1) & 0xFF)) << 8);
            case AddressingMode.IndirectX:
                int zx = (Fetch() + X) & 0xFF;
                return Read(zx) | (Read((zx + 1) & 0xFF) << 8);
            case AddressingMode.IndirectY:
                int zy = Fetch();
                baseAddress = Read(zy) | (Read((zy + 1) & 0xFF) << 8);
                address = (baseAddress + Y) & 0xFFFF;
                pageCrossed = (baseAddress & 0xFF00) != (address & 0xFF00);
                return address;
            default:
                throw new InvalidOperationException("Addressing mode has no effective address: " + mode);
        }
    }

    // --- ALU ---

    void Adc(byte value)
    {
        int carry = Carry ? 1 : 0;
        int binary = A + value + carry;
        Zero = (binary & 0xFF) == 0; // NMOS: Z always reflects the binary result

        if (Decimal)
        {
            int lo = (A & 0x0F) + (value & 0x0F) + carry;
            if (lo >= 0x0A) lo = ((lo + 0x06) & 0x0F) + 0x10;
            int sum = (A & 0xF0) + (value & 0xF0) + lo;
            Negative = (sum & 0x80) != 0;
            Overflow = ((~(A ^ value) & (A ^ sum)) & 0x80) != 0;
            if (sum >= 0xA0) sum += 0x60;
            Carry = sum >= 0x100;
            A = (byte)sum;
        }
        else
        {
            Overflow = ((~(A ^ value) & (A ^ binary)) & 0x80) != 0;
            Carry = binary > 0xFF;
            A = (byte)binary;
            Negative = (A & 0x80) != 0;
        }
    }

    void Sbc(byte value)
    {
        int borrow = Carry ? 0 : 1;
        int binary = A - value - borrow;
        byte result = (byte)binary;
        Overflow = (((A ^ value) & (A ^ binary)) & 0x80) != 0;
        Carry = binary >= 0;
        SetNZ(result); // NMOS: all flags reflect the binary result

        if (Decimal)
        {
            int lo = (A & 0x0F) - (value & 0x0F) - borrow;
            if (lo < 0) lo = ((lo - 0x06) & 0x0F) - 0x10;
            int sum = (A & 0xF0) - (value & 0xF0) + lo;
            if (sum < 0) sum -= 0x60;
            A = (byte)sum;
        }
        else
        {
            A = result;
        }
    }

    void Compare(byte register, byte value)
    {
        Carry = register >= value;
        SetNZ((byte)(register - value));
    }

    byte Asl(byte v) { Carry = (v & 0x80) != 0; v <<= 1; SetNZ(v); return v; }
    byte Lsr(byte v) { Carry = (v & 0x01) != 0; v >>= 1; SetNZ(v); return v; }
    byte Rol(byte v) { int c = Carry ? 1 : 0; Carry = (v & 0x80) != 0; v = (byte)((v << 1) | c); SetNZ(v); return v; }
    byte Ror(byte v) { int c = Carry ? 0x80 : 0; Carry = (v & 0x01) != 0; v = (byte)((v >> 1) | c); SetNZ(v); return v; }

    /// <summary>Executes a read-modify-write instruction on the accumulator or a memory location.</summary>
    void ReadModifyWrite(Instruction instruction, Func<byte, byte> operation)
    {
        if (instruction.Mode == AddressingMode.Accumulator)
        {
            A = operation(A);
            return;
        }

        int address = Address(instruction.Mode, out _);
        Write(address, operation(Read(address)));
    }

    bool Branch(bool condition)
    {
        sbyte offset = (sbyte)Fetch();
        if (!condition) return false;
        ushort old = PC;
        PC = (ushort)(PC + offset);
        Cycles += (old & 0xFF00) != (PC & 0xFF00) ? 2 : 1;
        return true;
    }

    /// <summary>Executes a single instruction.</summary>
    /// <returns>The number of cycles it took, 0 if the program is not running.</returns>
    public int Step()
    {
        if (!ProgramRunning) return 0;

        if (LimitProcessorSpeed)
            Thread.SpinWait(SpinWait);

        long startCycles = Cycles;
        ushort opcodeAddress = PC;
        byte opcode = Fetch();
        Instruction instruction = Opcodes.Decode(opcode)
            ?? throw new Exceptions.InvalidOpCodeException($"Opcode 0x{opcode:X2} at 0x{opcodeAddress:X4} is invalid.");

        Cycles += instruction.Cycles;
        Instructions++;
        bool crossed = false;
        int address;
        byte value;

        switch (instruction.Mnemonic)
        {
            // Loads
            case "LDA": A = ReadOperand(instruction, out crossed); SetNZ(A); break;
            case "LDX": X = ReadOperand(instruction, out crossed); SetNZ(X); break;
            case "LDY": Y = ReadOperand(instruction, out crossed); SetNZ(Y); break;

            // Stores (no page cross penalty, it is already in the base cycle count)
            case "STA": Write(Address(instruction.Mode, out _), A); break;
            case "STX": Write(Address(instruction.Mode, out _), X); break;
            case "STY": Write(Address(instruction.Mode, out _), Y); break;

            // Logic and arithmetic
            case "ORA": A |= ReadOperand(instruction, out crossed); SetNZ(A); break;
            case "AND": A &= ReadOperand(instruction, out crossed); SetNZ(A); break;
            case "EOR": A ^= ReadOperand(instruction, out crossed); SetNZ(A); break;
            case "ADC": Adc(ReadOperand(instruction, out crossed)); break;
            case "SBC": Sbc(ReadOperand(instruction, out crossed)); break;
            case "CMP": Compare(A, ReadOperand(instruction, out crossed)); break;
            case "CPX": Compare(X, ReadOperand(instruction, out crossed)); break;
            case "CPY": Compare(Y, ReadOperand(instruction, out crossed)); break;
            case "BIT":
                value = ReadOperand(instruction, out _);
                Zero = (A & value) == 0;
                Negative = (value & 0x80) != 0;
                Overflow = (value & 0x40) != 0;
                break;

            // Shifts, rotates, increments and decrements
            case "ASL": ReadModifyWrite(instruction, Asl); break;
            case "LSR": ReadModifyWrite(instruction, Lsr); break;
            case "ROL": ReadModifyWrite(instruction, Rol); break;
            case "ROR": ReadModifyWrite(instruction, Ror); break;
            case "INC": ReadModifyWrite(instruction, v => { v++; SetNZ(v); return v; }); break;
            case "DEC": ReadModifyWrite(instruction, v => { v--; SetNZ(v); return v; }); break;
            case "INX": X++; SetNZ(X); break;
            case "INY": Y++; SetNZ(Y); break;
            case "DEX": X--; SetNZ(X); break;
            case "DEY": Y--; SetNZ(Y); break;

            // Register transfers
            case "TAX": X = A; SetNZ(X); break;
            case "TAY": Y = A; SetNZ(Y); break;
            case "TXA": A = X; SetNZ(A); break;
            case "TYA": A = Y; SetNZ(A); break;
            case "TSX": X = SP; SetNZ(X); break;
            case "TXS": SP = X; break;

            // Stack
            case "PHA": Push(A); break;
            case "PHP": Push((byte)(Status | 0x10)); break;
            case "PLA": A = Pull(); SetNZ(A); break;
            case "PLP": Status = Pull(); break;

            // Flags
            case "CLC": Carry = false; break;
            case "SEC": Carry = true; break;
            case "CLI": InterruptDisable = false; break;
            case "SEI": InterruptDisable = true; break;
            case "CLD": Decimal = false; break;
            case "SED": Decimal = true; break;
            case "CLV": Overflow = false; break;

            // Branches
            case "BPL": Branch(!Negative); break;
            case "BMI": Branch(Negative); break;
            case "BVC": Branch(!Overflow); break;
            case "BVS": Branch(Overflow); break;
            case "BCC": Branch(!Carry); break;
            case "BCS": Branch(Carry); break;
            case "BNE": Branch(!Zero); break;
            case "BEQ": Branch(Zero); break;

            // Jumps and subroutines
            case "JMP": PC = (ushort)Address(instruction.Mode, out _); break;
            case "JSR":
                address = FetchWord();
                PushWord(PC - 1); // pushes the address of the last byte of the JSR
                PC = (ushort)address;
                break;
            case "RTS": PC = (ushort)(PullWord() + 1); break;
            case "RTI":
                Status = Pull();
                PC = (ushort)PullWord();
                break;
            case "BRK":
                Break = true;
                PC++; // BRK has a padding byte
                int vector = _memory.ReadWord(IrqVector);
                if (vector == 0)
                {
                    // No interrupt handler installed: treat BRK as "end of program".
                    ProgramRunning = false;
                    break;
                }
                Cycles -= 7; // Interrupt() adds them again
                Interrupt(IrqVector, breakFlag: true);
                break;

            case "NOP": break;

            default:
                throw new InvalidOperationException("Unhandled mnemonic " + instruction.Mnemonic);
        }

        if (crossed && instruction.PageCrossPenalty)
            Cycles++;

        return (int)(Cycles - startCycles);
    }

    byte ReadOperand(Instruction instruction, out bool pageCrossed) =>
        Read(Address(instruction.Mode, out pageCrossed));
}
