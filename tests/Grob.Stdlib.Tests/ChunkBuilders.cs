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

    internal static Chunk BuildCallChunk(string calleeName, params GrobValue[] args) {
        var chunk = new Chunk();
        int calleeIdx = chunk.AddConstant(GrobValue.FromString(calleeName));
        chunk.WriteOpCode(OpCode.GetGlobal, 1);
        chunk.WriteByte((byte)calleeIdx, 1);

        int[] argIndexes = [.. args.Select(chunk.AddConstant)];
        foreach (int argIdx in argIndexes) {
            chunk.WriteOpCode(OpCode.Constant, 1);
            chunk.WriteByte((byte)argIdx, 1);
        }
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte((byte)args.Length, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }
}
