using Grob.Core;
using Xunit;

namespace Grob.Core.Tests;

public class ChunkColumnTests {
    [Fact]
    public void GetColumn_DefaultOverloads_ReturnZero() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Return, 7);
        chunk.WriteByte(0xAB, 7);

        Assert.Equal(7, chunk.GetLine(0));
        Assert.Equal(0, chunk.GetColumn(0));
        Assert.Equal(7, chunk.GetLine(1));
        Assert.Equal(0, chunk.GetColumn(1));
    }

    [Fact]
    public void GetColumn_ColumnAwareOverloads_RoundtripPerByte() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, line: 12, column: 5);
        chunk.WriteByte(0x00, line: 12, column: 14);
        chunk.WriteOpCode(OpCode.Return, line: 13, column: 1);

        Assert.Equal((12, 5), (chunk.GetLine(0), chunk.GetColumn(0)));
        Assert.Equal((12, 14), (chunk.GetLine(1), chunk.GetColumn(1)));
        Assert.Equal((13, 1), (chunk.GetLine(2), chunk.GetColumn(2)));
    }
}
