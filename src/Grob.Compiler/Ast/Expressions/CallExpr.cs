using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>A function-call expression — <c>callee(arg, arg, ...)</c>.</summary>
/// <param name="Range">Source range covered by the whole call.</param>
/// <param name="Callee">The callee expression.</param>
/// <param name="Arguments">The arguments in source order, possibly with names attached.</param>
/// <param name="TypeArguments">
/// Explicit generic type arguments supplied at the call site (<c>a.mapAs&lt;Employee&gt;()</c>),
/// parsed by <see cref="Parser.ParsePostfix"/>'s <c>Less</c> case (D-416, closing D-415's Gap
/// A) via the unchanged <see cref="Parser.ParseTypeArgumentList"/>. Empty for every ordinary
/// call. Parser-supplied syntax, not a type-checker side channel — carries only
/// <see cref="TypeRef.Range"/> per node; resolving each argument to a concrete
/// <see cref="GrobType"/> and validating count/constraints (E0401/E0402) is a later
/// increment's job, deliberately left undone here.
/// </param>
public sealed record CallExpr(
    SourceRange Range,
    Expression Callee,
    IReadOnlyList<CallArgument> Arguments,
    IReadOnlyList<TypeRef> TypeArguments) : Expression(Range) {
    /// <summary>
    /// Every call site before D-416 — a positional record parameter default must be a
    /// compile-time constant (CS1736), which an empty <see cref="IReadOnlyList{T}"/>
    /// literal is not, so the "defaults to empty for an ordinary call" behaviour is
    /// carried by this overload rather than a <c>= []</c> parameter default.
    /// </summary>
    public CallExpr(SourceRange Range, Expression Callee, IReadOnlyList<CallArgument> Arguments)
        : this(Range, Callee, Arguments, []) {
    }

    /// <summary>
    /// Set by the type checker (Sprint 8 Increment E) when this call resolves to a
    /// <c>formatAs.table</c>/<c>list</c>/<c>csv</c> call — the function form
    /// (<c>formatAs.table(items)</c>) or the chained form (<c>items.formatAs.table()</c>),
    /// both resolved through the same <c>ResolveFormatAsCall</c>. Carries the ordered
    /// column-name list the compile-time field-registry lookup derived (possibly empty,
    /// never <see langword="null"/> once set), so the compiler can emit it as the
    /// synthesised second argument without re-deriving it — the runtime native never
    /// reflects over the value. <see langword="null"/> for every other call.
    /// </summary>
    public IReadOnlyList<string>? ResolvedFormatAsColumns { get; set; }

    /// <summary>
    /// Set by the type checker (D-362) to this call's statically resolved return type —
    /// mirroring <see cref="IndexExpr.ElementType"/> (D-359) and
    /// <see cref="MemberAccessExpr.ResolvedFieldType"/> — at every call shape whose return
    /// type is known: a direct user <c>FnDecl</c> call, a function-typed-variable call, a
    /// namespace-qualified native call, and a registered-named-type instance-method call.
    /// Stays <see cref="GrobType.Unknown"/> (the default) for a call whose result is
    /// genuinely unresolvable statically — a void-returning array higher-order method
    /// (<c>each</c>) or a call on an <c>Unknown</c>-typed receiver. The compiler's
    /// <c>GetExprType</c> reads this field directly rather than re-deriving the type.
    /// </summary>
    public GrobType ResolvedReturnType { get; set; } = GrobType.Unknown;

    /// <summary>
    /// Set by the type checker (D-380) when this call is one of the array/map methods
    /// with no meaningful return value — <c>each</c>, the array mutating members
    /// (<c>append</c>/<c>insert</c>/<c>remove</c>/<c>clear</c>) and the map mutating
    /// members (<c>set</c>/<c>remove</c>/<c>clear</c>). <see cref="ResolvedReturnType"/>
    /// alone cannot distinguish a genuinely void call from any other statically
    /// unresolvable call (both default to <see cref="GrobType.Unknown"/>), so
    /// <c>ResolveArithmetic</c> consults this flag to reject a void call used as an
    /// arithmetic operand (E0002) while staying permissive for the other Unknown sources.
    /// <see langword="false"/> for every other call.
    /// </summary>
    public bool IsVoidReturn { get; set; }

    /// <summary>
    /// Set by the type checker when this call resolves to a primitive-receiver
    /// instance-method call (D-066's compile-time-sugar model; the catalogue's primitive surface, D-426) —
    /// the qualified native name (e.g. <c>"string.split"</c>) the compiler rewrites the
    /// call to, receiver injected as arg[0]. <see langword="null"/> for every other call.
    /// </summary>
    public string? ResolvedPrimitiveNativeName { get; set; }

    /// <summary>
    /// Set by the type checker (D-365) alongside <see cref="ResolvedPrimitiveNativeName"/>
    /// to the resolved method signature's parameter defaults, one entry per parameter —
    /// the compile-time constants a call that omits trailing optional arguments is filled
    /// with. <see langword="null"/> whenever the resolved method declares no
    /// defaults (every primitive member but <c>padLeft</c>/<c>padRight</c>/<c>truncate</c>
    /// today) or this call is not a primitive-member call at all.
    /// </summary>
    public IReadOnlyList<GrobValue?>? ResolvedPrimitiveParameterDefaults { get; set; }

    /// <summary>
    /// Set by the type checker when this call is a direct call to a function declared in
    /// the script: its callee is an identifier that names the <see cref="FnDecl"/> itself.
    /// <see langword="null"/> for every other call, including a call through a parameter
    /// of function type, whose identifier also carries the enclosing <see cref="FnDecl"/>
    /// as its <see cref="IdentifierExpr.Declaration"/> (a parameter has no declaration node
    /// of its own). The compiler reads this, not the callee's declaration, to decide
    /// whether to bind the arguments against a declared parameter list.
    /// </summary>
    public FnDecl? ResolvedFunction { get; set; }

    /// <summary>
    /// Set by the type checker when this call's arguments bound cleanly to a declared
    /// parameter list and its arity holds on the bound set: the arguments in parameter
    /// order, each slot an argument of this call or a default. The compiler emits these
    /// slots in order rather than binding the call again. <see langword="null"/> for a
    /// call that does not bind, and for any call with a binding or arity error. Set today
    /// for a call to a script function (<see cref="ResolvedFunction"/>), a non-variadic
    /// namespace native and <c>input</c>.
    /// </summary>
    public BoundArgumentList? ResolvedBinding { get; set; }

    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitCall(this);
}
