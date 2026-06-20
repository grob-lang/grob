using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// A single arm of a switch expression — <c>pattern =&gt; result</c> (§3.1). The
/// <paramref name="Result"/> is evaluated and produced as the switch's value when
/// <paramref name="Pattern"/> is the first matching arm.
/// </summary>
/// <param name="Range">Source range covered by the arm.</param>
/// <param name="Pattern">The arm's pattern.</param>
/// <param name="Result">The expression producing the arm's value.</param>
public sealed record SwitchArm(
    SourceRange Range,
    SwitchPattern Pattern,
    Expression Result);
