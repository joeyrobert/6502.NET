using Emulator.Interpreter;
using Xunit;

namespace Emulator.Tests;

public class OpcodeCoverageTests
{
    public static IEnumerable<object[]> AllOpcodes() => Opcodes.All.Select(i => new object[] { i.Code });

    public static IEnumerable<object[]> UndocumentedOpcodes() =>
        Enumerable.Range(0, 256).Where(c => Opcodes.Decode((byte)c) is null).Select(c => new object[] { (byte)c });

    [Fact]
    public void TableHas151DocumentedOpcodes() => Assert.Equal(151, Opcodes.All.Count);

    [Fact]
    public void OpcodeTableHasNoDuplicateNamedForms()
    {
        var forms = Opcodes.All.Select(i => (i.Mnemonic, i.Mode)).ToList();
        Assert.Equal(forms.Count, forms.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(AllOpcodes))]
    public void EveryDocumentedOpcodeExecutes(byte code)
    {
        var instruction = Opcodes.Decode(code)!.Value;
        var memory = new Memory { RandomEnabled = false };
        // Operand bytes $10 $20 keep every mode in range; the pointer targets at $10/$11 and $2010 are zero.
        memory.Set(0x0600, new byte[] { code, 0x10, 0x20 });
        memory.Set(0x0010, new byte[] { 0x00, 0x30 });
        var cpu = new Processor(memory, 0x0600) { SP = 0xF0 };
        // Give the return/interrupt vectors somewhere valid to land.
        memory.Set(Processor.IrqVector, new byte[] { 0x00, 0x07 });
        memory.Set(0x01F1, new byte[] { 0x00, 0x06, 0x00, 0x06 });

        int cycles = cpu.Step();

        Assert.True(cycles >= instruction.Cycles, $"{instruction.Mnemonic} {instruction.Mode} took {cycles} cycles");
        Assert.Equal(1, cpu.Instructions);
    }

    [Theory]
    [MemberData(nameof(AllOpcodes))]
    public void NonBranchingInstructionsAdvancePcByTheirLength(byte code)
    {
        var instruction = Opcodes.Decode(code)!.Value;
        if (instruction.Mnemonic is "JMP" or "JSR" or "RTS" or "RTI" or "BRK") return;

        var memory = new Memory { RandomEnabled = false };
        memory.Set(0x0600, new byte[] { code, 0x10, 0x20 });
        memory.Set(0x0010, new byte[] { 0x00, 0x30 });
        var cpu = new Processor(memory, 0x0600) { SP = 0xF0 };
        // Make every conditional branch not taken.
        cpu.Negative = instruction.Mnemonic == "BPL";
        cpu.Zero = instruction.Mnemonic == "BNE";
        cpu.Overflow = instruction.Mnemonic == "BVC";
        cpu.Carry = instruction.Mnemonic == "BCC";

        cpu.Step();

        Assert.Equal(0x0600 + instruction.Length, cpu.PC);
    }

    [Theory]
    [MemberData(nameof(UndocumentedOpcodes))]
    public void UndocumentedOpcodesThrow(byte code)
    {
        var memory = new Memory();
        memory.Set(0x0600, code);
        var cpu = new Processor(memory, 0x0600);

        Assert.Throws<Exceptions.InvalidOpCodeException>(() => cpu.Step());
    }

    [Theory]
    [MemberData(nameof(AllOpcodes))]
    public void AssemblerRoundTripsEveryOpcode(byte code)
    {
        var instruction = Opcodes.Decode(code)!.Value;
        string operand = instruction.Mode switch
        {
            AddressingMode.Implied => "",
            AddressingMode.Accumulator => " A",
            AddressingMode.Immediate => " #$12",
            AddressingMode.ZeroPage => " $12",
            AddressingMode.ZeroPageX => " $12,X",
            AddressingMode.ZeroPageY => " $12,Y",
            AddressingMode.Absolute => " $1234",
            AddressingMode.AbsoluteX => " $1234,X",
            AddressingMode.AbsoluteY => " $1234,Y",
            AddressingMode.Indirect => " ($1234)",
            AddressingMode.IndirectX => " ($12,X)",
            AddressingMode.IndirectY => " ($12),Y",
            AddressingMode.Relative => " $0610",
            _ => throw new NotSupportedException(),
        };

        var program = Assembler.Assemble(instruction.Mnemonic + operand);

        Assert.Equal(code, program.Segments[0].Bytes[0]);
        Assert.Equal(instruction.Length, program.Length);
    }
}
