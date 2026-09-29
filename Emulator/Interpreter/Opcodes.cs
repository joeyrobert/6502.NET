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

/// <summary>6502 addressing modes.</summary>
public enum AddressingMode
{
    Implied,
    Accumulator,
    Immediate,
    ZeroPage,
    ZeroPageX,
    ZeroPageY,
    Absolute,
    AbsoluteX,
    AbsoluteY,
    Indirect,
    IndirectX,
    IndirectY,
    Relative,
}

/// <summary>
/// One entry of the opcode table.
/// </summary>
/// <param name="Code">Opcode byte.</param>
/// <param name="Mnemonic">Three letter mnemonic, upper case.</param>
/// <param name="Mode">Addressing mode.</param>
/// <param name="Cycles">Base cycle count.</param>
/// <param name="PageCrossPenalty">True if crossing a page boundary costs an extra cycle.</param>
public readonly record struct Instruction(byte Code, string Mnemonic, AddressingMode Mode, int Cycles, bool PageCrossPenalty)
{
    /// <summary>Instruction length in bytes, including the opcode.</summary>
    public int Length => Mode switch
    {
        AddressingMode.Implied or AddressingMode.Accumulator => 1,
        AddressingMode.Absolute or AddressingMode.AbsoluteX or AddressingMode.AbsoluteY or AddressingMode.Indirect => 3,
        _ => 2,
    };
}

/// <summary>
/// The 151 documented NMOS 6502 opcodes.
/// </summary>
public static class Opcodes
{
    static readonly Instruction?[] _table = new Instruction?[256];
    static readonly Dictionary<(string, AddressingMode), Instruction> _byName = new();

    /// <summary>All documented instructions, ordered by opcode.</summary>
    public static IReadOnlyList<Instruction> All { get; }

    static Opcodes()
    {
        const AddressingMode imp = AddressingMode.Implied, acc = AddressingMode.Accumulator,
            imm = AddressingMode.Immediate, zp = AddressingMode.ZeroPage, zpx = AddressingMode.ZeroPageX,
            zpy = AddressingMode.ZeroPageY, abs = AddressingMode.Absolute, abx = AddressingMode.AbsoluteX,
            aby = AddressingMode.AbsoluteY, ind = AddressingMode.Indirect, inx = AddressingMode.IndirectX,
            iny = AddressingMode.IndirectY, rel = AddressingMode.Relative;

        void Add(byte code, string mnemonic, AddressingMode mode, int cycles, bool pageCross = false)
        {
            var instruction = new Instruction(code, mnemonic, mode, cycles, pageCross);
            _table[code] = instruction;
            _byName[(mnemonic, mode)] = instruction;
        }

        // The eight "group one" ALU instructions share one layout: imm, zp, zpx, abs, absx, absy, indx, indy.
        void Alu(string mnemonic, byte imm_, byte zp_, byte zpx_, byte abs_, byte abx_, byte aby_, byte inx_, byte iny_)
        {
            Add(imm_, mnemonic, imm, 2);
            Add(zp_, mnemonic, zp, 3);
            Add(zpx_, mnemonic, zpx, 4);
            Add(abs_, mnemonic, abs, 4);
            Add(abx_, mnemonic, abx, 4, true);
            Add(aby_, mnemonic, aby, 4, true);
            Add(inx_, mnemonic, inx, 6);
            Add(iny_, mnemonic, iny, 5, true);
        }

        // Shifts, rotates and INC/DEC: (acc), zp, zpx, abs, absx.
        void Rmw(string mnemonic, int accCode, byte zp_, byte zpx_, byte abs_, byte abx_)
        {
            if (accCode >= 0) Add((byte)accCode, mnemonic, acc, 2);
            Add(zp_, mnemonic, zp, 5);
            Add(zpx_, mnemonic, zpx, 6);
            Add(abs_, mnemonic, abs, 6);
            Add(abx_, mnemonic, abx, 7);
        }

        Alu("ORA", 0x09, 0x05, 0x15, 0x0D, 0x1D, 0x19, 0x01, 0x11);
        Alu("AND", 0x29, 0x25, 0x35, 0x2D, 0x3D, 0x39, 0x21, 0x31);
        Alu("EOR", 0x49, 0x45, 0x55, 0x4D, 0x5D, 0x59, 0x41, 0x51);
        Alu("ADC", 0x69, 0x65, 0x75, 0x6D, 0x7D, 0x79, 0x61, 0x71);
        Alu("LDA", 0xA9, 0xA5, 0xB5, 0xAD, 0xBD, 0xB9, 0xA1, 0xB1);
        Alu("CMP", 0xC9, 0xC5, 0xD5, 0xCD, 0xDD, 0xD9, 0xC1, 0xD1);
        Alu("SBC", 0xE9, 0xE5, 0xF5, 0xED, 0xFD, 0xF9, 0xE1, 0xF1);

        Add(0x85, "STA", zp, 3);
        Add(0x95, "STA", zpx, 4);
        Add(0x8D, "STA", abs, 4);
        Add(0x9D, "STA", abx, 5);
        Add(0x99, "STA", aby, 5);
        Add(0x81, "STA", inx, 6);
        Add(0x91, "STA", iny, 6);

        Rmw("ASL", 0x0A, 0x06, 0x16, 0x0E, 0x1E);
        Rmw("ROL", 0x2A, 0x26, 0x36, 0x2E, 0x3E);
        Rmw("LSR", 0x4A, 0x46, 0x56, 0x4E, 0x5E);
        Rmw("ROR", 0x6A, 0x66, 0x76, 0x6E, 0x7E);
        Rmw("DEC", -1, 0xC6, 0xD6, 0xCE, 0xDE);
        Rmw("INC", -1, 0xE6, 0xF6, 0xEE, 0xFE);

        Add(0xA2, "LDX", imm, 2);
        Add(0xA6, "LDX", zp, 3);
        Add(0xB6, "LDX", zpy, 4);
        Add(0xAE, "LDX", abs, 4);
        Add(0xBE, "LDX", aby, 4, true);
        Add(0xA0, "LDY", imm, 2);
        Add(0xA4, "LDY", zp, 3);
        Add(0xB4, "LDY", zpx, 4);
        Add(0xAC, "LDY", abs, 4);
        Add(0xBC, "LDY", abx, 4, true);

        Add(0x86, "STX", zp, 3);
        Add(0x96, "STX", zpy, 4);
        Add(0x8E, "STX", abs, 4);
        Add(0x84, "STY", zp, 3);
        Add(0x94, "STY", zpx, 4);
        Add(0x8C, "STY", abs, 4);

        Add(0xE0, "CPX", imm, 2);
        Add(0xE4, "CPX", zp, 3);
        Add(0xEC, "CPX", abs, 4);
        Add(0xC0, "CPY", imm, 2);
        Add(0xC4, "CPY", zp, 3);
        Add(0xCC, "CPY", abs, 4);

        Add(0x24, "BIT", zp, 3);
        Add(0x2C, "BIT", abs, 4);

        Add(0x10, "BPL", rel, 2);
        Add(0x30, "BMI", rel, 2);
        Add(0x50, "BVC", rel, 2);
        Add(0x70, "BVS", rel, 2);
        Add(0x90, "BCC", rel, 2);
        Add(0xB0, "BCS", rel, 2);
        Add(0xD0, "BNE", rel, 2);
        Add(0xF0, "BEQ", rel, 2);

        Add(0x4C, "JMP", abs, 3);
        Add(0x6C, "JMP", ind, 5);
        Add(0x20, "JSR", abs, 6);
        Add(0x60, "RTS", imp, 6);
        Add(0x40, "RTI", imp, 6);
        Add(0x00, "BRK", imp, 7);
        Add(0xEA, "NOP", imp, 2);

        Add(0x48, "PHA", imp, 3);
        Add(0x08, "PHP", imp, 3);
        Add(0x68, "PLA", imp, 4);
        Add(0x28, "PLP", imp, 4);

        Add(0x18, "CLC", imp, 2);
        Add(0x38, "SEC", imp, 2);
        Add(0x58, "CLI", imp, 2);
        Add(0x78, "SEI", imp, 2);
        Add(0xB8, "CLV", imp, 2);
        Add(0xD8, "CLD", imp, 2);
        Add(0xF8, "SED", imp, 2);

        Add(0xAA, "TAX", imp, 2);
        Add(0xA8, "TAY", imp, 2);
        Add(0xBA, "TSX", imp, 2);
        Add(0x8A, "TXA", imp, 2);
        Add(0x9A, "TXS", imp, 2);
        Add(0x98, "TYA", imp, 2);
        Add(0xCA, "DEX", imp, 2);
        Add(0x88, "DEY", imp, 2);
        Add(0xE8, "INX", imp, 2);
        Add(0xC8, "INY", imp, 2);

        All = _table.Where(i => i.HasValue).Select(i => i!.Value).ToArray();
    }

    /// <summary>Looks up an opcode byte. Returns null for undocumented opcodes.</summary>
    public static Instruction? Decode(byte code) => _table[code];

    /// <summary>Looks up an instruction by mnemonic and addressing mode.</summary>
    public static bool TryGet(string mnemonic, AddressingMode mode, out Instruction instruction) =>
        _byName.TryGetValue((mnemonic.ToUpperInvariant(), mode), out instruction);

    /// <summary>True if the mnemonic exists in any addressing mode.</summary>
    public static bool IsMnemonic(string mnemonic) =>
        All.Any(i => i.Mnemonic.Equals(mnemonic, StringComparison.OrdinalIgnoreCase));
}
