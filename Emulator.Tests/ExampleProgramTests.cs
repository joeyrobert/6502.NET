using Emulator.Interpreter;
using Xunit;

namespace Emulator.Tests;

public class ExampleProgramTests
{
    static (Memory Memory, Processor Cpu) Run(string name, long maxInstructions = 5_000_000)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Examples", name + ".asm");
        var program = Assembler.AssembleFile(path);
        var memory = new Memory { RandomEnabled = false };
        program.Load(memory);
        var cpu = new Processor(memory, (ushort)program.Entry);
        cpu.Run(maxInstructions);
        return (memory, cpu);
    }

    public static IEnumerable<object[]> Examples() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Examples"), "*.asm")
            .Select(f => new object[] { Path.GetFileNameWithoutExtension(f) });

    [Theory]
    [MemberData(nameof(Examples))]
    public void EveryExampleAssemblesAndRunsWithoutErrors(string name)
    {
        var (_, cpu) = Run(name, 200_000);
        Assert.True(cpu.Instructions > 0);
    }

    [Fact]
    public void FibonacciStoresTheSequence()
    {
        var (memory, cpu) = Run("fibonacci");
        Assert.False(cpu.ProgramRunning);
        var expected = new byte[] { 0, 1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144 };
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], memory.Read(0x10 + i));
    }

    [Fact]
    public void PrimesSieveFindsAllPrimesBelow128()
    {
        var (memory, cpu) = Run("primes");
        int[] primes = Enumerable.Range(2, 126).Where(n => Enumerable.Range(2, n - 2).All(d => n % d != 0)).ToArray();
        Assert.False(cpu.ProgramRunning);
        Assert.Equal(primes.Length, memory.Read(0x00));
        Assert.Equal(primes, Enumerable.Range(0, primes.Length).Select(i => (int)memory.Read(0x1100 + i)));
    }

    [Fact]
    public void BubbleSortSortsAscending()
    {
        var (memory, cpu) = Run("bubble_sort");
        Assert.False(cpu.ProgramRunning);
        var sorted = Enumerable.Range(0, 10).Select(i => memory.Read(0x80 + i)).ToArray();
        Assert.Equal(new byte[] { 0, 1, 3, 5, 7, 9, 17, 42, 200, 250 }, sorted);
    }

    [Fact]
    public void MultiplyComputes200Times150()
    {
        var (memory, cpu) = Run("multiply");
        Assert.False(cpu.ProgramRunning);
        Assert.Equal(30000, memory.Read(0x02) | (memory.Read(0x03) << 8));
    }

    [Fact]
    public void BcdCounterCountsInDecimal()
    {
        var (memory, cpu) = Run("bcd_counter");
        Assert.Equal(0x01, memory.Read(0x0201));
        Assert.Equal(0x09, memory.Read(0x0209));
        Assert.Equal(0x10, memory.Read(0x020A));
        Assert.Equal(0x99, memory.Read(0x0200 + 99));
        Assert.Equal(0x00, cpu.A);
        Assert.True(cpu.Carry);
    }

    [Fact]
    public void StackDemoReachesTheIndirectJumpTarget()
    {
        var (memory, _) = Run("stack_demo");
        Assert.Equal(24, memory.Read(0x00));
        Assert.Equal(0x00, memory.Read(0x01));
        Assert.Equal(0xAA, memory.Read(0x02));
    }

    [Fact]
    public void GradientFillsTheWholeScreen()
    {
        var (memory, cpu) = Run("gradient");
        Assert.False(cpu.ProgramRunning);
        for (int row = 0; row < 32; row++)
            for (int col = 0; col < 32; col++)
                Assert.Equal((row + col) & 0xFF, memory.Read(0x200 + row * 32 + col));
    }

    [Fact]
    public void DiscoAndRandomDotsPaintTheScreen()
    {
        var (disco, _) = Run("disco", 5_000);
        Assert.Contains(Enumerable.Range(0x200, 0x400), i => disco.Read(i) != 0);

        var mem = new Memory(); // random enabled
        Assembler.AssembleFile(Path.Combine(AppContext.BaseDirectory, "Examples", "random_dots.asm")).Load(mem);
        new Processor(mem, 0x600).Run(5_000);
        Assert.Contains(Enumerable.Range(0x200, 0x400), i => mem.Read(i) != 0);
    }

    static byte Pixel(Memory memory, int x, int y) => (byte)(memory.Read(0x200 + y * 32 + x) & 0x0F);

    [Fact]
    public void SierpinskiLightsPixelsWhereXAndYAreDisjoint()
    {
        var (memory, cpu) = Run("sierpinski");
        Assert.False(cpu.ProgramRunning);
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                Assert.Equal((x & y) == 0, Pixel(memory, x, y) != 0);
    }

    [Fact]
    public void SmileyDrawsTheScaledSprite()
    {
        var (memory, cpu) = Run("smiley");
        Assert.False(cpu.ProgramRunning);
        Assert.Equal(6, Pixel(memory, 0, 0));   // blue corner
        Assert.Equal(7, Pixel(memory, 16, 2));  // yellow forehead
        Assert.Equal(6, Pixel(memory, 9, 9));   // eye
        Assert.Equal(6, Pixel(memory, 15, 25)); // mouth
    }

    [Fact]
    public void TunnelDrawsConcentricRingsThatCycleEachFrame()
    {
        var (memory, _) = Run("tunnel", 27_876 * 3); // three complete frames
        // Frame counter is 3 here, so the last complete frame used 2.
        Assert.Equal(2, Pixel(memory, 0, 0));
        Assert.Equal(2, Pixel(memory, 31, 31));
        Assert.Equal(3, Pixel(memory, 1, 20));
        Assert.Equal((15 + 2) & 0x0F, Pixel(memory, 16, 16));
    }

    [Fact]
    public void BouncingBallKeepsExactlyOnePixelLitAndStaysOnScreen()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Examples", "bouncing_ball.asm");
        var program = Assembler.AssembleFile(path);
        var memory = new Memory { RandomEnabled = false };
        program.Load(memory);
        var cpu = new Processor(memory, (ushort)program.Entry);
        var seen = new HashSet<(int, int)>();
        for (int i = 0; i < 400; i++)
        {
            cpu.Run(1_500); // roughly one frame
            var lit = Enumerable.Range(0, 1024).Where(p => memory.Read(0x200 + p) != 0).ToList();
            Assert.True(lit.Count <= 1);
            foreach (int p in lit) seen.Add((p % 32, p / 32));
        }
        Assert.True(seen.Count > 20);
        Assert.Contains(seen, p => p.Item1 == 0 || p.Item1 == 31);
    }

    [Fact]
    public void PngExportProducesAValidImage()
    {
        var (memory, _) = Run("smiley");
        byte[] png = Palette.ToPng(memory, 2);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
        Assert.Equal(64, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
        Assert.Equal(64, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
    }
}
