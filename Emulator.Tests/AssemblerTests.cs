using Emulator.Interpreter;
using Xunit;

namespace Emulator.Tests;

public class AssemblerTests
{
    static byte[] Bytes(string source) => Assembler.Assemble(source).Segments.SelectMany(s => s.Bytes).ToArray();

    [Fact]
    public void AssemblesAllOperandForms() =>
        Assert.Equal(new byte[]
        {
            0xA9, 0x01, 0xA5, 0x02, 0xB5, 0x03, 0xAD, 0x34, 0x12, 0xBD, 0x34, 0x12, 0xB9, 0x34, 0x12,
            0xA1, 0x04, 0xB1, 0x05, 0x6C, 0x00, 0x03, 0x0A, 0x0A, 0xB6, 0x07,
        }, Bytes("""
            lda #1
            lda 2
            lda 3,x
            lda $1234
            lda $1234,x
            lda $1234,y
            lda (4,x)
            lda (5),y
            jmp ($0300)
            asl a
            asl
            ldx 7,y
            """));

    [Fact]
    public void NumberFormatsAndOperators() =>
        Assert.Equal(new byte[] { 10, 10, 10, 10, 65, 0x34, 0x12, 0x34, 0x12, 0x11 },
            Bytes(".byte 10, $0a, 0x0A, %1010, 'A'\n.word $1234\nlabel = $1234\n.byte <label, >label\n.byte 3+$10-2"));

    [Fact]
    public void LabelsCommentsAndForwardReferences()
    {
        var program = Assembler.Assemble("""
            start:  jmp end     ; forward reference
            middle: nop
            end:    beq start
            """);
        Assert.Equal(new byte[] { 0x4C, 0x04, 0x06, 0xEA, 0xF0, 0xFA }, program.Segments[0].Bytes);
        Assert.Equal(0x0604, program.Symbols["end"]);
    }

    [Fact]
    public void ForwardReferencesAreAbsoluteAndBackwardZeroPageConstantsAreZeroPage()
    {
        // The size must be decided in pass one, before a forward label's value is known.
        Assert.Equal(new byte[] { 0xAD, 0x03, 0x06, 0xA5, 0x10 }, Bytes("zp = $10\nlda later\nlater: lda zp"));
    }

    [Fact]
    public void OrgCreatesSegmentsAndFillAndTextWork()
    {
        var program = Assembler.Assemble(".org $0200\n.text \"hi\"\n.fill 2, $ff\n.org $0300\n.byte 1");
        Assert.Equal(2, program.Segments.Count);
        Assert.Equal(0x0200, program.Segments[0].Address);
        Assert.Equal(new byte[] { (byte)'h', (byte)'i', 0xFF, 0xFF }, program.Segments[0].Bytes);
        Assert.Equal(0x0300, program.Segments[1].Address);
        Assert.Equal(new byte[] { 1 }, program.Segments[1].Bytes);
    }

    [Fact]
    public void SemicolonInsideStringIsNotAComment() =>
        Assert.Equal(new byte[] { (byte)'a', (byte)';', (byte)'b' }, Bytes(".text \"a;b\" ; real comment"));

    [Theory]
    [InlineData("foo")]
    [InlineData("lda #")]
    [InlineData("lda ($10,y)")]
    [InlineData("sta #1")]
    [InlineData("lda #$1ff")]
    [InlineData("bne nowhere")]
    [InlineData("lda missing")]
    [InlineData(".bogus 1")]
    [InlineData("x: nop\nx: nop")]
    public void ErrorsAreReported(string source) => Assert.Throws<AssemblyException>(() => Assembler.Assemble(source));

    [Fact]
    public void BranchOutOfRangeIsAnError()
    {
        var ex = Assert.Throws<AssemblyException>(() => Assembler.Assemble("bne far\n.fill 200\nfar: nop"));
        Assert.Equal(1, ex.Line);
    }

    [Fact]
    public void OriginCanBeOverridden()
    {
        var program = Assembler.Assemble("lab: jmp lab", origin: 0x8000);
        Assert.Equal(0x8000, program.Origin);
        Assert.Equal(new byte[] { 0x4C, 0x00, 0x80 }, program.Segments[0].Bytes);
    }
}
