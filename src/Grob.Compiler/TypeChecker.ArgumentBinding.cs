using Grob.Compiler.Ast;
using Grob.Compiler.Binding;
using Grob.Core;

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
    /// always had.
    /// </summary>
    private readonly record struct BindingWording(string Name, string Subject) {
        public static BindingWording ForScriptFunction(string name) => new(name, $"Function '{name}'");
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
    /// The record a script-function call hands the compiler: each slot the argument
    /// bound to it, or a default the compiler emits from the declaration's default
    /// expression.
    /// </summary>
    private static BoundArgumentList ScriptBoundArguments(ArgumentBinding binding) =>
        new([.. binding.SlotArgument.Select(a => new BoundSlot(
            a == ArgumentBinder.Unbound ? BoundSlot.DefaultArgument : a, NativeDefault: null))]);
}
