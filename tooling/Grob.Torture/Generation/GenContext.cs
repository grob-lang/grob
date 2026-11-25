namespace Grob.Torture.Generation;

/// <summary>What one program's generation shares: its random choices, the natives, and the types and functions it has declared.</summary>
/// <param name="random">The program's choices.</param>
/// <param name="members">The natives it may call.</param>
internal sealed class GenContext(GenRandom random, MemberTable members) {
    private int _nextName;

    /// <summary>The program's choices.</summary>
    public GenRandom Random => random;

    /// <summary>The natives it may call.</summary>
    public MemberTable Members => members;

    /// <summary>The struct types declared so far, in declaration order.</summary>
    public List<GenType.Struct> Structs { get; } = [];

    /// <summary>The functions declared so far, in declaration order.</summary>
    public List<GenFunction> Functions { get; } = [];

    /// <summary>How many names the program has used so far.</summary>
    public int NamesUsed => _nextName;

    /// <summary>A name used nowhere else in the program.</summary>
    public string Fresh(string prefix) => $"{prefix}{_nextName++}";
}
