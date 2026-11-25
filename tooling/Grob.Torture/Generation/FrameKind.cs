namespace Grob.Torture.Generation;

/// <summary>What opened a frame of a <see cref="GenScope"/>.</summary>
internal enum FrameKind {
    /// <summary>The top level or a function body: the outermost frame.</summary>
    Root,

    /// <summary>The body of an <c>if</c> or <c>else</c>.</summary>
    Block,

    /// <summary>The body of a <c>while</c> or <c>for…in</c>.</summary>
    Loop,

    /// <summary>A <c>select</c> arm.</summary>
    Select,

    /// <summary>A <c>try</c>, <c>catch</c> or <c>finally</c> block.</summary>
    Try,

    /// <summary>A <c>finally</c> block, which a <c>return</c> may not leave (E2207).</summary>
    Finally,

    /// <summary>A lambda's body.</summary>
    Lambda,
}
