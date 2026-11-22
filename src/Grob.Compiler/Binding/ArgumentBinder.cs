using Grob.Compiler.Ast;

namespace Grob.Compiler.Binding;

/// <summary>
/// The one D-113 binding routine (D-446 Decision 1). It binds a call's arguments to any
/// parameter list that supplies a name and a has-default flag per position, so a script
/// function and a native are bound by the same loop and there is nothing native-specific
/// to bind.
/// </summary>
/// <remarks>
/// <para>
/// Binding runs in source order. A positional argument fills the next leading slot; a
/// named argument fills the slot of the parameter it names, provided that parameter has
/// a default and is not already filled. Each break of the convention is recorded with
/// the argument it is reported at, in argument order, which is the order the checker's
/// diagnostics have always come out in. Binding carries on past a problem so every
/// independent one surfaces.
/// </para>
/// <para>
/// The binder emits no diagnostic. The wording differs by callee (<c>Function 'f'</c>
/// for a script function, <c>'date.parse'</c> for a native), and keeping it out of the
/// loop is what lets the loop be shared without moving any script-function message.
/// </para>
/// </remarks>
internal static class ArgumentBinder {
    /// <summary>A slot no argument was bound to.</summary>
    public const int Unbound = -1;

    /// <summary>
    /// Binds <paramref name="arguments"/> to <paramref name="parameters"/>.
    /// </summary>
    public static ArgumentBinding Bind<TParameters>(IReadOnlyList<CallArgument> arguments, TParameters parameters)
            where TParameters : IBindableParameters {
        int[] slots = new int[parameters.Count];
        Array.Fill(slots, Unbound);
        List<BindingProblem> problems = [];
        int positionalCount = 0;
        bool seenNamed = false;

        for (int i = 0; i < arguments.Count; i++) {
            string? name = arguments[i].Name;
            if (name is not null) {
                seenNamed = true;
                BindNamed(parameters, slots, problems, name, i);
            } else if (seenNamed) {
                // A positional after a named argument binds nowhere and is not counted.
                problems.Add(new BindingProblem(BindingProblemKind.PositionalAfterNamed, i));
            } else {
                if (positionalCount < slots.Length) slots[positionalCount] = i;
                positionalCount++;
            }
        }

        return new ArgumentBinding(slots, positionalCount, LeavesRequiredUnbound(parameters, slots), problems);
    }

    private static void BindNamed<TParameters>(TParameters parameters, int[] slots, List<BindingProblem> problems,
            string name, int argumentIndex)
            where TParameters : IBindableParameters {
        int p = IndexOf(parameters, name);
        if (ProblemWith(parameters, slots, p) is { } kind) {
            problems.Add(new BindingProblem(kind, argumentIndex));
            return;
        }
        slots[p] = argumentIndex;
    }

    /// <summary>
    /// Why a named argument cannot bind to parameter <paramref name="p"/>, or
    /// <see langword="null"/> when it can. The checks run in the order the checker has
    /// always made them: unknown name, then required parameter, then already supplied.
    /// </summary>
    private static BindingProblemKind? ProblemWith<TParameters>(TParameters parameters, int[] slots, int p)
            where TParameters : IBindableParameters {
        if (p < 0) return BindingProblemKind.Unknown;
        if (!parameters.HasDefaultAt(p)) return BindingProblemKind.NamesRequired;
        if (slots[p] != Unbound) return BindingProblemKind.Duplicate;
        return null;
    }

    private static int IndexOf<TParameters>(TParameters parameters, string name)
            where TParameters : IBindableParameters {
        for (int i = 0; i < parameters.Count; i++) {
            if (parameters.NameAt(i) == name) return i;
        }
        return -1;
    }

    private static bool LeavesRequiredUnbound<TParameters>(TParameters parameters, int[] slots)
            where TParameters : IBindableParameters {
        for (int i = 0; i < slots.Length; i++) {
            if (slots[i] == Unbound && !parameters.HasDefaultAt(i)) return true;
        }
        return false;
    }
}
