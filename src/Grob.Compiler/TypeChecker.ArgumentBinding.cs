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
