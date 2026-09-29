using Emulator.Interpreter;
using Xunit;

namespace Emulator.Tests;

/// <summary>
/// Runs Klaus Dormann's 6502 functional test suite (https://github.com/Klaus2m5/6502_65C02_functional_tests)
/// if 6502_functional_test.bin is present in TestData. CI downloads it; locally the test is skipped without it.
/// The binary is GPL licensed and is therefore not stored in this repository.
/// </summary>
public class FunctionalTest
{
    const string Path = "TestData/6502_functional_test.bin";
    const int Start = 0x0400;
    const int SuccessTrap = 0x3469; // address the suite loops on after passing every test

    [SkippableFact]
    public void KlausDormannFunctionalTestPasses()
    {
        Skip.IfNot(File.Exists(Path), "6502_functional_test.bin not present");

        var memory = new Memory { RandomEnabled = false };
        memory.Set(0, File.ReadAllBytes(Path));
        var cpu = new Processor(memory, Start);

        int previous = -1;
        long steps = 0;
        while (steps++ < 100_000_000)
        {
            previous = cpu.PC;
            cpu.Step();
            if (cpu.PC == previous) break; // a jump-to-self is the suite's way of reporting a result
        }

        Assert.True(cpu.PC == SuccessTrap, $"Functional test trapped at 0x{cpu.PC:X4} after {cpu.Instructions} instructions");
    }
}
