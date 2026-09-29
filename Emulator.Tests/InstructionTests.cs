using Emulator.Interpreter;
using Xunit;

namespace Emulator.Tests;

public class InstructionTests
{
    [Fact]
    public void LoadsSetFlags()
    {
        var m = Machine.Run("lda #$80\nldx #0\nldy #$7f\nbrk");
        Assert.Equal(0x80, m.Cpu.A);
        Assert.Equal(0, m.Cpu.X);
        Assert.Equal(0x7F, m.Cpu.Y);
        Assert.False(m.Cpu.Negative); // last load was Y=$7f
        Assert.False(m.Cpu.Zero);
    }

    [Fact]
    public void AllStoreAndLoadAddressingModesAgree()
    {
        var m = Machine.Run("""
            ldx #3
            ldy #4
            lda #$11
            sta $10
            sta $20,x       ; $23
            sta $0300
            sta $0300,x     ; $0303
            sta $0300,y     ; $0304
            lda #$00
            sta $40
            lda #$05
            sta $41         ; pointer $0500
            lda #$77
            sta ($3d,x)     ; ($40) -> $0500 (0x3d + 3 = 0x40)
            sta ($40),y     ; $0504
            ldx #0
            ldy #0
            lda $0500
            sta $60
            lda $0504
            sta $61
            stx $70
            sty $71
            brk
            """);
        Assert.Equal(0x11, m[0x10]);
        Assert.Equal(0x11, m[0x23]);
        Assert.Equal(0x11, m[0x0300]);
        Assert.Equal(0x11, m[0x0303]);
        Assert.Equal(0x11, m[0x0304]);
        Assert.Equal(0x77, m[0x60]);
        Assert.Equal(0x77, m[0x61]);
    }

    [Fact]
    public void ZeroPageIndexingWrapsWithinThePage()
    {
        var m = Machine.Run("ldx #$10\nlda #$99\nsta $f8,x\nbrk"); // $f8+$10 -> $08
        Assert.Equal(0x99, m[0x08]);
        Assert.Equal(0x00, m[0x0108]);
    }

    [Theory]
    [InlineData(0x50, 0x10, false, 0x60, false, false)]
    [InlineData(0x50, 0x50, false, 0xA0, false, true)]  // signed overflow
    [InlineData(0xFF, 0x01, false, 0x00, true, false)]
    [InlineData(0xFF, 0x00, true, 0x00, true, false)]
    [InlineData(0x80, 0x80, false, 0x00, true, true)]
    public void AdcBinary(int a, int operand, bool carryIn, int result, bool carryOut, bool overflow)
    {
        var m = Machine.Run($"{(carryIn ? "sec" : "clc")}\nlda #{a}\nadc #{operand}\nbrk");
        Assert.Equal(result, m.Cpu.A);
        Assert.Equal(carryOut, m.Cpu.Carry);
        Assert.Equal(overflow, m.Cpu.Overflow);
        Assert.Equal(result == 0, m.Cpu.Zero);
        Assert.Equal((result & 0x80) != 0, m.Cpu.Negative);
    }

    [Theory]
    [InlineData(0x50, 0x10, true, 0x40, true, false)]
    [InlineData(0x50, 0x10, false, 0x3F, true, false)]  // borrow
    [InlineData(0x00, 0x01, true, 0xFF, false, false)]
    [InlineData(0x50, 0xB0, true, 0xA0, false, true)]   // signed overflow
    public void SbcBinary(int a, int operand, bool carryIn, int result, bool carryOut, bool overflow)
    {
        var m = Machine.Run($"{(carryIn ? "sec" : "clc")}\nlda #{a}\nsbc #{operand}\nbrk");
        Assert.Equal(result, m.Cpu.A);
        Assert.Equal(carryOut, m.Cpu.Carry);
        Assert.Equal(overflow, m.Cpu.Overflow);
    }

    [Theory]
    [InlineData(0x09, 0x01, 0x10, false)]
    [InlineData(0x58, 0x46, 0x04, true)]   // 58 + 46 = 104
    [InlineData(0x99, 0x01, 0x00, true)]
    [InlineData(0x12, 0x34, 0x46, false)]
    public void AdcDecimal(int a, int operand, int result, bool carry)
    {
        var m = Machine.Run($"sed\nclc\nlda #${a:X2}\nadc #${operand:X2}\nbrk");
        Assert.Equal(result, m.Cpu.A);
        Assert.Equal(carry, m.Cpu.Carry);
    }

    [Theory]
    [InlineData(0x46, 0x12, 0x34, true)]
    [InlineData(0x40, 0x13, 0x27, true)]
    [InlineData(0x00, 0x01, 0x99, false)]
    public void SbcDecimal(int a, int operand, int result, bool carry)
    {
        var m = Machine.Run($"sed\nsec\nlda #${a:X2}\nsbc #${operand:X2}\nbrk");
        Assert.Equal(result, m.Cpu.A);
        Assert.Equal(carry, m.Cpu.Carry);
    }

    [Fact]
    public void LogicalOperations()
    {
        var m = Machine.Run("lda #$f0\nand #$3c\nsta $00\nora #$03\nsta $01\neor #$ff\nsta $02\nbrk");
        Assert.Equal(0x30, m[0]);
        Assert.Equal(0x33, m[1]);
        Assert.Equal(0xCC, m[2]);
    }

    [Fact]
    public void BitCopiesBitsAndTestsMask()
    {
        var m = Machine.Run("lda #$01\nbit $10\nbrk", x => x.Memory.Set(0x10, 0xC0));
        Assert.True(m.Cpu.Zero);
        Assert.True(m.Cpu.Negative);
        Assert.True(m.Cpu.Overflow);
    }

    [Theory]
    [InlineData(0x40, 0x40, true, true, false)]
    [InlineData(0x40, 0x41, false, false, true)]
    [InlineData(0x40, 0x3F, true, false, false)]
    public void CompareSetsCarryZeroNegative(int a, int operand, bool carry, bool zero, bool negative)
    {
        foreach (string op in new[] { "cmp", "cpx", "cpy" })
        {
            string load = op switch { "cmp" => "lda", "cpx" => "ldx", _ => "ldy" };
            var m = Machine.Run($"{load} #{a}\n{op} #{operand}\nbrk");
            Assert.Equal(carry, m.Cpu.Carry);
            Assert.Equal(zero, m.Cpu.Zero);
            Assert.Equal(negative, m.Cpu.Negative);
            Assert.Equal(a, op switch { "cmp" => m.Cpu.A, "cpx" => m.Cpu.X, _ => m.Cpu.Y }); // compare must not modify
        }
    }

    [Fact]
    public void ShiftsAndRotates()
    {
        var m = Machine.Run("""
            lda #$81
            asl a
            sta $00        ; $02, carry set
            rol a
            sta $01        ; $05, carry clear
            lsr a
            sta $02        ; $02, carry set
            ror a
            sta $03        ; $81, carry clear
            brk
            """);
        Assert.Equal(0x02, m[0]);
        Assert.Equal(0x05, m[1]);
        Assert.Equal(0x02, m[2]);
        Assert.Equal(0x81, m[3]);
        Assert.False(m.Cpu.Carry);
    }

    [Fact]
    public void ShiftsOnMemoryModifyMemoryNotAccumulator()
    {
        var m = Machine.Run("lda #$55\nasl $10\nlsr $11\nrol $12\nror $13\nasl $0300\nlsr $0301,x\nbrk",
            x => { x.Memory.Set(0x10, 0x81); x.Memory.Set(0x11, 0x03); x.Memory.Set(0x12, 0x80); x.Memory.Set(0x13, 0x01);
                   x.Memory.Set(0x300, 0x40); x.Memory.Set(0x301, 0x08); });
        Assert.Equal(0x55, m.Cpu.A);
        Assert.Equal(0x02, m[0x10]);
        Assert.Equal(0x01, m[0x11]);
        Assert.Equal(0x01, m[0x12]); // carry from the previous LSR rotates in
        Assert.Equal(0x80, m[0x13]);
        Assert.Equal(0x80, m[0x300]);
        Assert.Equal(0x04, m[0x301]);
    }

    [Fact]
    public void IncrementAndDecrementWrapAndSetFlags()
    {
        var m = Machine.Run("ldx #$ff\ninx\nphp\nldy #0\ndey\nphp\ninc $10\ndec $11\ninc $0300,x\ndec $20,x\nbrk",
            x => { x.Memory.Set(0x10, 0xFF); x.Memory.Set(0x11, 0x00); });
        Assert.Equal(0x00, m.Cpu.X);
        Assert.Equal(0xFF, m.Cpu.Y);
        Assert.Equal(0x00, m[0x10]);
        Assert.Equal(0xFF, m[0x11]);
        Assert.Equal(0x01, m[0x0300]);
        Assert.Equal(0xFF, m[0x20]);
    }

    [Fact]
    public void RegisterTransfers()
    {
        var m = Machine.Run("lda #$42\ntax\ntay\nldx #$ff\ntxs\ntsx\ntxa\nbrk");
        Assert.Equal(0xFF, m.Cpu.SP);
        Assert.Equal(0xFF, m.Cpu.A);
        Assert.Equal(0x42, m.Cpu.Y);
        Assert.True(m.Cpu.Negative);
        var m2 = Machine.Run("ldy #$00\ntya\nbrk", x => x.Cpu.A = 5);
        Assert.Equal(0, m2.Cpu.A);
        Assert.True(m2.Cpu.Zero);
    }

    [Fact]
    public void TxsDoesNotAffectFlags()
    {
        var m = Machine.Run("ldx #0\nlda #1\ntxs\nbrk");
        Assert.False(m.Cpu.Zero); // lda #1 cleared it; txs with X=0 must not set it
    }

    [Fact]
    public void FlagInstructions()
    {
        var m = Machine.Run("sec\nsed\nsei\nphp\nclc\ncld\ncli\nbrk");
        Assert.False(m.Cpu.Carry);
        Assert.False(m.Cpu.Decimal);
        Assert.False(m.Cpu.InterruptDisable);
        var v = Machine.Run("clv\nbrk", x => x.Cpu.Overflow = true);
        Assert.False(v.Cpu.Overflow);
    }

    [Fact]
    public void StackPushAndPull()
    {
        var m = Machine.Run("lda #$12\npha\nlda #$34\npha\npla\nsta $00\npla\nsta $01\nbrk");
        Assert.Equal(0x34, m[0]);
        Assert.Equal(0x12, m[1]);
        Assert.Equal(0xFD, m.Cpu.SP);
        Assert.Equal(0x12, m[0x01FD]);
        Assert.Equal(0x34, m[0x01FC]);
    }

    [Fact]
    public void PhpPushesBreakFlagAndPlpIgnoresIt()
    {
        var m = Machine.Run("sec\nphp\nclc\nplp\nbrk");
        Assert.True(m.Cpu.Carry);
        Assert.Equal(0x35, m[0x01FD]); // C | I | U | B  (I set from reset state)
    }

    [Fact]
    public void JsrPushesReturnAddressMinusOneAndRtsReturns()
    {
        var m = Machine.Run("""
            jsr sub
            lda #1
            sta $00
            brk
            sub: ldx #7
            rts
            """);
        Assert.Equal(7, m.Cpu.X);
        Assert.Equal(1, m[0]);
        Assert.Equal(0x06, m[0x01FD]);
        Assert.Equal(0x02, m[0x01FC]); // $0600 + 3 - 1
    }

    [Fact]
    public void JmpAbsoluteAndIndirect()
    {
        var m = Machine.Run("""
            jmp there
            lda #$ff
            there: jmp (ptr)
            lda #$ff
            end: ldx #9
            brk
            ptr: .word end
            """);
        Assert.Equal(9, m.Cpu.X);
        Assert.NotEqual(0xFF, m.Cpu.A);
    }

    [Fact]
    public void JmpIndirectWrapsWithinThePageOnPageBoundary()
    {
        // Pointer at $02FF: low byte from $02FF, high byte from $0200 (not $0300) on NMOS parts.
        var m = Machine.Run("jmp ($02ff)\n.org $0700\nldx #5\nbrk", x =>
        {
            x.Memory.Set(0x02FF, 0x00);
            x.Memory.Set(0x0200, 0x07);
            x.Memory.Set(0x0300, 0x09);
        });
        Assert.Equal(5, m.Cpu.X);
    }

    [Theory]
    [InlineData("bne", "ldx #1", true)]
    [InlineData("bne", "ldx #0", false)]
    [InlineData("beq", "ldx #0", true)]
    [InlineData("beq", "ldx #1", false)]
    [InlineData("bcc", "clc", true)]
    [InlineData("bcc", "sec", false)]
    [InlineData("bcs", "sec", true)]
    [InlineData("bcs", "clc", false)]
    [InlineData("bmi", "ldx #$80", true)]
    [InlineData("bmi", "ldx #1", false)]
    [InlineData("bpl", "ldx #1", true)]
    [InlineData("bpl", "ldx #$80", false)]
    [InlineData("bvs", "lda #$40\nsta $10\nbit $10", true)]
    [InlineData("bvs", "clv", false)]
    [InlineData("bvc", "clv", true)]
    [InlineData("bvc", "lda #$40\nsta $10\nbit $10", false)]
    public void BranchesTakenOnlyWhenConditionHolds(string branch, string setup, bool taken)
    {
        var m = Machine.Run($"{setup}\n{branch} target\nldy #1\njmp done\ntarget: ldy #2\ndone: brk");
        Assert.Equal(taken ? 2 : 1, m.Cpu.Y);
    }

    [Fact]
    public void BackwardBranchesWork()
    {
        var m = Machine.Run("ldx #5\nloop: dex\nbne loop\nbrk");
        Assert.Equal(0, m.Cpu.X);
    }

    [Fact]
    public void BrkWithoutHandlerHaltsTheProgram()
    {
        var m = Machine.Run("lda #1\nbrk\nlda #2");
        Assert.False(m.Cpu.ProgramRunning);
        Assert.Equal(1, m.Cpu.A);
        Assert.True(m.Cpu.Break);
    }

    [Fact]
    public void BrkWithHandlerPushesStateAndRtiReturns()
    {
        var m = Machine.Run("""
            lda #$11
            brk
            .byte $00       ; padding byte
            ldx #$22        ; resumed here after RTI
            stx $00
            lda #0
            sta $fffe       ; remove the handler so the final BRK halts
            sta $ffff
            brk
            .org $0700
            handler: ldy #$33
            rti
            """, x =>
        {
            x.Memory.Set(Processor.IrqVector, new byte[] { 0x00, 0x07 });
            x.Cpu.SP = 0xFD;
        });
        Assert.Equal(0x22, m[0]);
        Assert.Equal(0x33, m.Cpu.Y);
        Assert.Equal(0xFD, m.Cpu.SP);
    }

    [Fact]
    public void BrkPushesReturnAddressPlusTwoAndBreakFlag()
    {
        var mem = new Memory { RandomEnabled = false };
        mem.Set(0x600, new byte[] { 0x00 });
        mem.Set(Processor.IrqVector, new byte[] { 0x00, 0x07 });
        var cpu = new Processor(mem, 0x600);
        cpu.Step();
        Assert.Equal(0x0700, cpu.PC);
        Assert.Equal(0x06, mem.Read(0x1FD));
        Assert.Equal(0x02, mem.Read(0x1FC));
        Assert.Equal(0x30, mem.Read(0x1FB) & 0x30); // B and U set
        Assert.True(cpu.InterruptDisable);
    }

    [Fact]
    public void IrqIsMaskedByInterruptDisableAndNmiIsNot()
    {
        var mem = new Memory { RandomEnabled = false };
        mem.Set(Processor.IrqVector, new byte[] { 0x00, 0x07 });
        mem.Set(Processor.NmiVector, new byte[] { 0x00, 0x08 });
        var cpu = new Processor(mem, 0x600);

        cpu.Irq(); // I is set after reset
        Assert.Equal(0x600, cpu.PC);

        cpu.InterruptDisable = false;
        cpu.Irq();
        Assert.Equal(0x700, cpu.PC);
        Assert.Equal(0x00, mem.Read(0x1FB) & 0x10); // B clear for hardware interrupts

        cpu.Nmi();
        Assert.Equal(0x800, cpu.PC);
    }

    [Fact]
    public void ResetLoadsProgramCounterFromVector()
    {
        var mem = new Memory { RandomEnabled = false };
        mem.Set(Processor.ResetVector, new byte[] { 0x34, 0x12 });
        var cpu = new Processor(mem) { A = 5 };
        cpu.Reset();
        Assert.Equal(0x1234, cpu.PC);
        Assert.Equal(0, cpu.A);
        Assert.Equal(0xFD, cpu.SP);
        Assert.Equal(0x24, cpu.Status);
    }

    [Fact]
    public void CycleCountsIncludePageCrossAndBranchPenalties()
    {
        var mem = new Memory { RandomEnabled = false };
        mem.Set(0x600, new byte[] { 0xBD, 0xFF, 0x02,   // LDA $02FF,X
                                    0xBD, 0x00, 0x02,   // LDA $0200,X
                                    0xD0, 0x00,         // BNE +0 (taken)
                                    0xF0, 0x00 });      // BEQ +0 (not taken)
        mem.Set(0x300, 1);
        mem.Set(0x201, 1);
        var cpu = new Processor(mem, 0x600) { X = 1 };
        Assert.Equal(5, cpu.Step()); // page crossed
        Assert.Equal(4, cpu.Step());
        Assert.Equal(3, cpu.Step()); // branch taken, same page
        Assert.Equal(2, cpu.Step()); // not taken
    }

    [Fact]
    public void BranchAcrossPageCostsFourCycles()
    {
        var mem = new Memory { RandomEnabled = false };
        mem.Set(0x6FD, new byte[] { 0xD0, 0x10 }); // BNE from $06FD -> $070F crosses a page
        var cpu = new Processor(mem, 0x6FD);
        Assert.Equal(4, cpu.Step());
        Assert.Equal(0x070F, cpu.PC);
    }

    [Fact]
    public void StoresDoNotIncurPageCrossPenalty()
    {
        var mem = new Memory { RandomEnabled = false };
        mem.Set(0x600, new byte[] { 0x9D, 0xFF, 0x02 });
        var cpu = new Processor(mem, 0x600) { X = 1 };
        Assert.Equal(5, cpu.Step());
    }

    [Fact]
    public void NopDoesNothing()
    {
        var m = Machine.Run("lda #7\nnop\nnop\nbrk");
        Assert.Equal(7, m.Cpu.A);
        Assert.Equal(4, m.Cpu.Instructions);
    }

    [Fact]
    public void RandomByteAddressReturnsVaryingValues()
    {
        var mem = new Memory(); // random enabled by default
        var values = Enumerable.Range(0, 64).Select(_ => mem.Read(Memory.RandomByteAddress)).Distinct();
        Assert.True(values.Count() > 8);
    }

    [Fact]
    public void MemoryBoundsAreEnforced()
    {
        var mem = new Memory(0x100);
        Assert.Throws<Exceptions.MemoryOutOfBoundsException>(() => mem.Read(0x100));
        Assert.Throws<Exceptions.MemoryOutOfBoundsException>(() => mem.Set(-1, 0));
        Assert.Throws<Exceptions.MemoryOutOfBoundsException>(() => mem.Set(0xFF, new byte[] { 1, 2 }));
    }
}
