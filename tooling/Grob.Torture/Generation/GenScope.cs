namespace Grob.Torture.Generation;

/// <summary>
/// The bindings visible where the generator is writing, frame by frame, and what the frames
/// allow. Names are unique within a program, so nothing is shadowed.
/// </summary>
/// <param name="returnType">The enclosing function's return type, or <see langword="null"/> at the top level.</param>
/// <param name="visibleFunctions">
/// How many of the program's functions this code may call: all of them at the top level, and only
/// those declared before it in a function body, so no function can reach itself (an unbounded
/// recursion overflows the stack, F-3).
/// </param>
internal sealed class GenScope(GenType? returnType, int visibleFunctions) {
    private readonly List<(FrameKind Kind, List<GenVariable> Variables)> _frames = [(FrameKind.Root, [])];

    /// <summary>The enclosing function's return type, or <see langword="null"/> at the top level.</summary>
    public GenType? ReturnType => returnType;

    /// <summary>How many of the program's functions this code may call, in declaration order.</summary>
    public int VisibleFunctions => visibleFunctions;

    /// <summary>Opens a frame.</summary>
    public void Push(FrameKind kind) => _frames.Add((kind, []));

    /// <summary>Closes the innermost frame and forgets its bindings.</summary>
    public void Pop() => _frames.RemoveAt(_frames.Count - 1);

    /// <summary>Adds a binding to the innermost frame.</summary>
    public void Declare(GenVariable variable) => _frames[^1].Variables.Add(variable);

    /// <summary>Every binding in view.</summary>
    public IEnumerable<GenVariable> Readable => _frames.SelectMany(f => f.Variables);

    /// <summary>The bindings in view of <paramref name="type"/>.</summary>
    public IEnumerable<GenVariable> OfType(GenType type) => Readable.Where(v => v.Type == type);

    /// <summary>
    /// The bindings a statement here may assign. A lambda assigns nothing outside itself, and a
    /// loop body assigns only numbers and flags from outside the loop: a string or an array
    /// grown on every pass of nested loops would grow without bound.
    /// </summary>
    public IEnumerable<GenVariable> Assignable {
        get {
            int lambda = _frames.FindLastIndex(f => f.Kind == FrameKind.Lambda);
            int loop = _frames.FindLastIndex(f => f.Kind == FrameKind.Loop);
            return _frames
                .SelectMany((f, i) => f.Variables.Select(v => (Index: i, Variable: v)))
                .Where(e => e.Variable.Assignable && e.Index >= lambda && (e.Index >= loop || IsBounded(e.Variable.Type)))
                .Select(e => e.Variable);
        }
    }

    /// <summary>Whether a <c>break</c> or <c>continue</c> here would leave a loop and nothing else.</summary>
    public bool DirectlyInLoop => _frames.FindLast(f => f.Kind != FrameKind.Block).Kind == FrameKind.Loop;

    /// <summary>Whether any loop encloses this point within the current function or lambda.</summary>
    public bool InLoop {
        get {
            int lambda = _frames.FindLastIndex(f => f.Kind == FrameKind.Lambda);
            return _frames.FindLastIndex(f => f.Kind == FrameKind.Loop) > lambda;
        }
    }

    /// <summary>Whether this point is inside a <c>finally</c> block.</summary>
    public bool InFinally => _frames.Exists(f => f.Kind == FrameKind.Finally);

    /// <summary>Whether this point is inside a lambda's body.</summary>
    public bool InLambda => _frames.Exists(f => f.Kind == FrameKind.Lambda);

    private static bool IsBounded(GenType type) => type == GenType.Int || type == GenType.Float || type == GenType.Bool;
}
