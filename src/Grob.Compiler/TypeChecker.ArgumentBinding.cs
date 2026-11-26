using Grob.Compiler.Ast;
using Grob.Compiler.Binding;
using Grob.Core;
using Grob.Runtime;

namespace Grob.Compiler;

public sealed partial class TypeChecker {
    // -----------------------------------------------------------------------
    // Named-argument binding diagnostics (D-113, D-446 Decision 1). The binding
    // itself is ArgumentBinder's; this turns its problems into diagnostics worded
    // for the callee, and its slots into the record the compiler emits from.
    // -----------------------------------------------------------------------

    /// <summary>
    /// How the binding diagnostics name a callee. <paramref name="Name"/> appears in
    /// quotes in E0008, E0009 and E0010; <paramref name="Subject"/> opens E0011. A script
    /// function is <c>'f'</c> and <c>Function 'f'</c>, the wording these four codes have
    /// always had. A native is named as its arity messages name it (<c>'date.parse'</c>,
    /// <c>'input'</c>), and its E0011 drops "Function", as those messages do (gate report
    /// §1.4): the same sentences, so there is one convention (D-446 Decision 1).
    /// </summary>
    private readonly record struct BindingWording(string Name, string Subject) {
        public static BindingWording ForScriptFunction(string name) => new(name, $"Function '{name}'");

        public static BindingWording ForNative(string name) => new(name, $"'{name}'");
    }

    /// <summary>
    /// Whether an argument of <paramref name="argumentType"/> bound to a parameter of
    /// <paramref name="parameterType"/> is the one implicit widening the checker accepts,
    /// <c>int</c> to <c>float</c>, which the compiler must make real with
    /// <see cref="OpCode.IntToFloat"/> (R-58).
    /// </summary>
    private static bool Widens(GrobType argumentType, GrobType parameterType) =>
        argumentType == GrobType.Int && parameterType == GrobType.Float;

    /// <summary>
    /// Records, on a call to a callee that has no signature to bind against, which of its
    /// arguments widen (<paramref name="widen"/>, one entry per argument, in source order): a
    /// function value, whose type names its parameters' types but no names (D-326), and an
    /// array or map method, whose parameter is the receiver's element or value type. The
    /// record is a <see cref="BoundArgumentList"/> that binds every argument to the slot in its
    /// own position, so the compiler emits it with the one routine that emits every other bound
    /// call. A call that widens nothing, <paramref name="widen"/> being <see langword="null"/>
    /// until an argument does, keeps no record, and its bytes are what they were.
    /// </summary>
    private static void RecordArgumentWidening(CallExpr node, bool[]? widen) {
        if (widen is null) return;
        var slots = new BoundSlot[widen.Length];
        for (int a = 0; a < slots.Length; a++) {
            slots[a] = new BoundSlot(a, NativeDefault: null, WidenToFloat: widen[a]);
        }
        node.ResolvedBinding = new BoundArgumentList(slots);
    }

    /// <summary>
    /// <see cref="RecordArgumentWidening"/> for a call whose one widening argument is at
    /// <paramref name="argumentIndex"/>, bound to <paramref name="targetType"/>: the element
    /// type of an array method's receiver or the value type of a map method's. A receiver whose
    /// type the checker does not know (<paramref name="targetType"/> is <see langword="null"/>)
    /// records nothing.
    /// </summary>
    private static void RecordWideningAt(CallExpr node, int argumentIndex, GrobType argType, GrobType? targetType) {
        if (targetType is not { } target || !Widens(argType, target)) return;
        var widen = new bool[node.Arguments.Count];
        widen[argumentIndex] = true;
        RecordArgumentWidening(node, widen);
    }

    /// <summary>
    /// Reports each of <paramref name="binding"/>'s problems at the argument it names,
    /// in argument order. Returns <see langword="true"/> when there was any, so the
    /// caller skips the arity and type checks (one diagnostic per root cause).
    /// </summary>
    private bool ReportBindingProblems(CallExpr node, ArgumentBinding binding, BindingWording callee) {
        foreach (BindingProblem problem in binding.Problems) {
            CallArgument arg = node.Arguments[problem.ArgumentIndex];
            switch (problem.Kind) {
                case BindingProblemKind.PositionalAfterNamed:
                    EmitError(ErrorCatalog.E0008,
                        $"In the call to '{callee.Name}', a positional argument follows a named argument. Move all named arguments after the positional ones.",
                        arg.Value.Range);
                    break;
                case BindingProblemKind.NamesRequired:
                    EmitError(ErrorCatalog.E0009,
                        $"Parameter '{arg.Name}' of '{callee.Name}' is required and has no default, so it cannot be passed by name. Pass it positionally.",
                        arg.Range);
                    break;
                case BindingProblemKind.Duplicate:
                    EmitError(ErrorCatalog.E0010,
                        $"Parameter '{arg.Name}' of '{callee.Name}' is already supplied.",
                        arg.Range);
                    break;
                case BindingProblemKind.Unknown:
                    EmitError(ErrorCatalog.E0011,
                        $"{callee.Subject} has no parameter named '{arg.Name}'.",
                        arg.Range);
                    break;
            }
        }
        return binding.Problems.Count > 0;
    }

    /// <summary>
    /// Reports E0003 for a call whose binding left a parameter with no default unbound,
    /// naming each such parameter in parameter order at the call. The callee opens the
    /// message as <see cref="BindingWording.Subject"/> does, so a script function reads
    /// <c>Function 'f' expects ...</c> and a native <c>'date.parse' expects ...</c>, the
    /// frame every E0003 message shares. Too many positionals keeps the count wording.
    /// </summary>
    private void EmitMissingRequiredError<TParameters>(
            CallExpr node, BindingWording callee, TParameters parameters, ArgumentBinding binding)
            where TParameters : IBindableParameters {
        IReadOnlyList<string> names = ArgumentBinder.UnboundRequiredNames(parameters, binding);
        string listed = QuotedList(names);
        string message = names.Count == 1
            ? $"{callee.Subject} expects an argument for required parameter {listed}, but none was supplied."
            : $"{callee.Subject} expects arguments for required parameters {listed}, but none were supplied.";
        EmitError(ErrorCatalog.E0003, message, node.Range);
    }

    /// <summary>
    /// Renders <paramref name="names"/> quoted, comma-separated with "and" before the last
    /// and no comma before it: <c>'a'</c>, <c>'a' and 'b'</c>, <c>'a', 'b' and 'c'</c>.
    /// </summary>
    private static string QuotedList(IReadOnlyList<string> names) {
        if (names.Count == 1) return $"'{names[0]}'";
        string head = string.Join(", ", names.Take(names.Count - 1).Select(n => $"'{n}'"));
        return $"{head} and '{names[^1]}'";
    }

    /// <summary>
    /// The record a clean call hands the compiler: each slot the argument bound to it,
    /// marked where it widens (<paramref name="widen"/>, one entry per parameter), or the
    /// parameter's default. A native's default is its signature constant
    /// (<paramref name="signature"/>); a script function passes <see langword="null"/>,
    /// and the compiler emits the declaration's default expression instead.
    /// </summary>
    private static BoundArgumentList BoundArguments(ArgumentBinding binding, bool[] widen, FunctionSignature? signature) {
        var slots = new BoundSlot[binding.SlotArgument.Count];
        for (int p = 0; p < slots.Length; p++) {
            int a = binding.SlotArgument[p];
            slots[p] = a == ArgumentBinder.Unbound
                ? new BoundSlot(BoundSlot.DefaultArgument, signature?.Parameters[p].Default)
                : new BoundSlot(a, NativeDefault: null, WidenToFloat: widen[p]);
        }
        return new BoundArgumentList(slots);
    }
}
