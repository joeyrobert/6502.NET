using Emulator.Interpreter;

namespace Emulator.Tests;

/// <summary>Assembles a snippet at $0600, runs it until BRK and exposes the machine state.</summary>
public sealed class Machine
{
    public Memory Memory { get; } = new() { RandomEnabled = false };
    public Processor Cpu { get; }

    public Machine(string source, Action<Machine>? setup = null)
    {
        var program = Assembler.Assemble(source);
        program.Load(Memory);
        Cpu = new Processor(Memory, (ushort)program.Entry);
        setup?.Invoke(this);
        Cpu.Run(1_000_000);
    }

    public static Machine Run(string source, Action<Machine>? setup = null) => new(source, setup);

    public byte this[int address] => Memory.Read(address);
}
