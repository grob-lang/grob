using Grob.Core;
using Xunit;

namespace Grob.Core.Tests;

public class ChunkTryRegionTests {
    [Fact]
    public void AddTryRegion_ReturnsSequentialIndices() {
        var chunk = new Chunk();
        int first = chunk.AddTryRegion();
        int second = chunk.AddTryRegion();

        Assert.Equal(0, first);
        Assert.Equal(1, second);
        Assert.Equal(2, chunk.TryRegionCount);
    }

    [Fact]
    public void SetTryRegion_ThenGetTryRegion_RoundtripsBoundsAndHandlers() {
        var chunk = new Chunk();
        int index = chunk.AddTryRegion();

        CatchHandler handler = new(["IoError"], IsCatchAll: false, HandlerOffset: 42, BindingSlot: 3);
        TryRegion region = new(StartOffset: 5, EndOffset: 20, Handlers: [handler]);
        chunk.SetTryRegion(index, region);

        TryRegion roundtripped = chunk.GetTryRegion(index);
        Assert.Equal(5, roundtripped.StartOffset);
        Assert.Equal(20, roundtripped.EndOffset);
        CatchHandler h = Assert.Single(roundtripped.Handlers);
        Assert.Equal(["IoError"], h.MatchTypeNames);
        Assert.False(h.IsCatchAll);
        Assert.Equal(42, h.HandlerOffset);
        Assert.Equal(3, h.BindingSlot);
    }

    [Fact]
    public void AddTryRegion_Overflow_Throws() {
        var chunk = new Chunk();
        for (int i = 0; i < 256; i++) chunk.AddTryRegion();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => chunk.AddTryRegion());
        Assert.Contains("256", ex.Message);
    }
}
