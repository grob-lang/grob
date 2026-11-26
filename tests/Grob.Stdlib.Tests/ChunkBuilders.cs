using Grob.Core;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Shared hand-built-chunk helpers for the stdlib plugin test fixtures (Math/Path/
/// StringsPluginTests) — a bare <see cref="OpCode.GetGlobal"/> read and a
/// <see cref="OpCode.GetGlobal"/>-then-<see cref="OpCode.Call"/> with N constant
/// arguments. Extracted so the bytecode shape lives in one place as it evolves, rather
/// than drifting across near-identical copies in each fixture.
/// </summary>
internal static class ChunkBuilders {
    internal static Chunk BuildGetGlobalChunk(string name) {
        var chunk = new Chunk();
        int idx = chunk.AddConstant(GrobValue.FromString(name));
        chunk.WriteOpCode(OpCode.GetGlobal, 1);
        chunk.WriteByte((byte)idx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    internal static Chunk BuildCallChunk(string calleeName, params GrobValue[] args) =>
        BuildCallChunkAt(1, 0, calleeName, args);

    /// <summary>
    /// As <see cref="BuildCallChunk"/>, with every instruction at <paramref name="line"/> and
    /// <paramref name="column"/>, so a test can assert where a native's error is reported.
    /// </summary>
    internal static Chunk BuildCallChunkAt(int line, int column, string calleeName, params GrobValue[] args) {
        var chunk = new Chunk();
        int calleeIdx = chunk.AddConstant(GrobValue.FromString(calleeName));
        chunk.WriteOpCode(OpCode.GetGlobal, line, column);
        chunk.WriteByte((byte)calleeIdx, line, column);

        int[] argIndexes = [.. args.Select(chunk.AddConstant)];
        foreach (int argIdx in argIndexes) {
            chunk.WriteOpCode(OpCode.Constant, line, column);
            chunk.WriteByte((byte)argIdx, line, column);
        }
        chunk.WriteOpCode(OpCode.Call, line, column);
        chunk.WriteByte((byte)args.Length, line, column);
        chunk.WriteOpCode(OpCode.Return, line, column);
        return chunk;
    }
}
