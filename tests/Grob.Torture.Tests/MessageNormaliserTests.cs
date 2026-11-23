using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// An exception's message loses its values and keeps its shape, so two programs with one root
/// cause share a signature (gate report §4.7). Kind and type names stay: they discriminate.
/// </summary>
public sealed class MessageNormaliserTests {
    [Theory]
    [InlineData("index 5 of 12", "index # of #")]
    [InlineData("value -3.5e10 overflowed", "value # overflowed")]
    [InlineData("opcode GetProperty 'padLeft' on receiver of kind String", "opcode GetProperty '…' on receiver of kind String")]
    [InlineData("zone \"abc\" not found", "zone \"…\" not found")]
    [InlineData(@"cannot read C:\work\a.grob now", "cannot read <path> now")]
    [InlineData("cannot read /home/u/a.grob now", "cannot read <path> now")]
    [InlineData("GrobValue kind mismatch: expected Float, actual Int.", "GrobValue kind mismatch: expected Float, actual Int.")]
    [InlineData("slot12 and E0101", "slot12 and E0101")]
    public void Normalise_RemovesValuesAndKeepsShape(string message, string expected) {
        Assert.Equal(expected, MessageNormaliser.Normalise(message));
    }
}
