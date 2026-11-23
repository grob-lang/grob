using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// The test helper's own contract: <see cref="ChunkRendering.AssertEmits"/> pins the whole
/// constant pool from the indices the instructions' operands name, never from text inside
/// a rendered constant's value.
/// </summary>
public sealed class ChunkRenderingTests {
    [Fact]
    public void AssertEmits_StringConstantThatLooksLikeAPoolIndex_IsNotReadAsOne() =>
        ChunkRendering.AssertEmits("""v := "[999] " """, """
            Constant [0] String "[999] "
            DefineGlobal [1] String "v"
            Return
            """);
}
