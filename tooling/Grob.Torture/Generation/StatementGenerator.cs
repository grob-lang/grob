using System.Globalization;

namespace Grob.Torture.Generation;

/// <summary>
/// Writes statements (prompt §2 group 5): bindings, assignments, <c>print</c>, <c>if</c>,
/// <c>while</c>, <c>for…in</c>, <c>select</c>, <c>try</c>/<c>catch</c>/<c>finally</c> with
/// <c>throw</c>, <c>break</c>, <c>continue</c> and <c>return</c>. Every program it writes ends on its
/// own: a <c>while</c> counts up to a small bound from a counter its body cannot assign, and bumps
/// the counter first, so a <c>continue</c> cannot skip it; a loop body appends to no array.
/// </summary>
internal sealed class StatementGenerator(GenContext context, ExpressionGenerator expressions) {
    /// <summary>
    /// The deepest a statement may sit. The rest of <see cref="ProgramGenerator.MaxNesting"/> is
    /// kept for its expressions, so an expression of any type the generator uses still fits.
    /// </summary>
    public const int MaxStatementDepth = ProgramGenerator.MaxNesting - 4;

    private static readonly string[] ThrownErrors = ["ArithmeticError", "IoError", "IndexError", "LookupError", "RuntimeError"];

    private GenRandom Random => context.Random;

    /// <summary>
    /// The names a program may use before its blocks stop growing. The top level's global names
    /// share its constant pool with its literals and member names, indexed by a one-byte operand,
    /// so a large enough program stops the compiler (a limit for the campaign's triage, not a shape
    /// for this generator to reach). The budget is a proxy for the pool's size, measured to leave
    /// every program of a 3,000-program run inside the limit.
    /// </summary>
    public const int NameBudget = 100;

    /// <summary>Writes up to <paramref name="count"/> statements at <paramref name="depth"/>, fewer once <see cref="NameBudget"/> is spent.</summary>
    public void Block(CodeWriter writer, GenScope scope, int depth, int count) {
        for (int i = 0; i < count && context.NamesUsed < NameBudget; i++) {
            Statement(writer, scope, depth);
        }
    }

    /// <summary>A type for a new binding, a parameter or a field.</summary>
    /// <param name="allowFunction">Whether a function type may be chosen.</param>
    /// <param name="allowStruct">Whether a declared struct type may be chosen.</param>
    public GenType PickType(bool allowFunction, bool allowStruct = true) {
        List<GenType.Struct> structs = allowStruct ? context.Structs : [];
        return Random.Weighted<Func<GenType>>([
            (22, () => GenType.Int),
            (8, () => GenType.Float),
            (14, () => GenType.String),
            (8, () => GenType.Bool),
            (10, () => new GenType.ArrayOf(PickScalar())),
            (structs.Count > 0 ? 3 : 0, () => new GenType.ArrayOf(Random.Pick(structs))),
            (5, () => new GenType.MapOf(PickScalar())),
            (structs.Count > 0 ? 10 : 0, () => Random.Pick(structs)),
            (8, () => new GenType.Nullable(PickScalar())),
            (structs.Count > 0 ? 6 : 0, () => new GenType.Nullable(Random.Pick(structs))),
            (allowFunction ? 5 : 0, PickFunctionType),
        ])();
    }

    /// <summary>
    /// A type for an array's element: a scalar or a struct. Never nullable, since the checker
    /// refuses an array literal that mixes <c>nil</c> with a value, and never an array or a map,
    /// since it judges an inner array of an annotated binding and one of an inferred binding to differ.
    /// </summary>
    public GenType PickElementType() {
        GenType type = PickType(allowFunction: false);
        return type switch {
            GenType.Nullable nullable => nullable.Inner,
            GenType.ArrayOf or GenType.MapOf => PickScalar(),
            _ => type,
        };
    }

    /// <summary>A function type over scalars.</summary>
    public GenType.Function PickFunctionType() =>
        new([.. Enumerable.Range(0, Random.Between(0, 2)).Select(_ => PickScalar())], PickScalar());

    /// <summary>One of the four scalar types.</summary>
    public GenType PickScalar() => Random.Weighted<GenType>([(4, GenType.Int), (2, GenType.Float), (3, GenType.String), (1, GenType.Bool)]);

    private void Statement(CodeWriter writer, GenScope scope, int depth) {
        Action write = Random.Weighted<Action>([.. FlatStatements(writer, scope, depth), .. NestedStatements(writer, scope, depth)]);
        write();
    }

    // The two lists are one weighted choice, split only to keep each readable; their order is
    // part of what a seed fixes, so neither is reordered.
    private List<(int Weight, Action Write)> FlatStatements(CodeWriter writer, GenScope scope, int depth) {
        List<GenVariable> assignable = [.. scope.Assignable];
        return [
            (30, () => Binding(writer, scope, depth)),
            (15, () => Print(writer, scope, depth)),
            (assignable.Count > 0 ? 10 : 0, () => Assignment(writer, scope, depth, assignable)),
            (assignable.Any(v => IsNumber(v.Type)) ? 6 : 0, () => Compound(writer, scope, depth, assignable)),
            (assignable.Any(v => v.Type == GenType.Int) ? 4 : 0, () => Increment(writer, assignable)),
            (4, () => FunctionBinding(writer, scope, depth)),
            (3, () => AnonymousBinding(writer, scope, depth)),
            (!scope.InLoop && scope.Assignable.Any(v => v.Type is GenType.ArrayOf) ? 3 : 0, () => Append(writer, scope, depth)),
        ];
    }

    private List<(int Weight, Action Write)> NestedStatements(CodeWriter writer, GenScope scope, int depth) {
        bool nest = depth + 1 <= MaxStatementDepth;
        return [
            (nest ? 8 : 0, () => If(writer, scope, depth)),
            (nest ? 4 : 0, () => While(writer, scope, depth)),
            (nest ? 4 : 0, () => ForRange(writer, scope, depth)),
            (nest ? 4 : 0, () => ForArray(writer, scope, depth)),
            (nest && scope.Readable.Any(v => v.Type is GenType.MapOf) ? 3 : 0, () => ForMap(writer, scope, depth)),
            (nest ? 4 : 0, () => Select(writer, scope, depth)),
            (nest && !scope.InLambda ? 4 : 0, () => Try(writer, scope, depth)),
            (nest && scope.DirectlyInLoop ? 4 : 0, () => LoopExit(writer, scope, depth)),
            (nest && scope.ReturnType is not null && !scope.InLambda && !scope.InFinally ? 2 : 0, () => EarlyReturn(writer, scope, depth)),
        ];
    }

    /// <summary>
    /// A binding. A nullable, function or struct type is always annotated: <c>x := nil</c> has no
    /// type to infer, a lambda needs the annotation's shape, and a struct copied by <c>b := a</c>
    /// loses its type's name, so its fields read back as unknown.
    /// </summary>
    public void Binding(CodeWriter writer, GenScope scope, int depth, GenType? type = null) {
        type ??= PickType(allowFunction: false);
        string name = context.Fresh("v");
        bool annotate = type is GenType.Nullable or GenType.Function or GenType.Struct || Random.Chance(30);
        string value = expressions.Expr(type, depth + 1, scope);
        writer.Line(annotate ? $"{name}: {type.Annotation} := {value}" : $"{name} := {value}");
        // A function binding is never reassigned: a lambda on the right of '=' has no annotation to type its parameters.
        scope.Declare(new GenVariable(name, type, Assignable: type is not GenType.Function));
    }

    private void Print(CodeWriter writer, GenScope scope, int depth) {
        GenType type = Random.Chance(80) ? PickScalar() : new GenType.Nullable(PickScalar());
        writer.Line($"print({expressions.Expr(type, depth + 1, scope)})");
    }

    private void Assignment(CodeWriter writer, GenScope scope, int depth, List<GenVariable> assignable) {
        GenVariable target = Random.Pick(assignable);
        string place = target.Name;
        GenType type = target.Type;
        if (target.Type is GenType.ArrayOf array && Random.Chance(50)) {
            place = $"{target.Name}[{Random.Between(0, 2).ToString(CultureInfo.InvariantCulture)}]";
            type = array.Element;
        } else if (target.Type is GenType.Struct structure && structure.Fields.Any(f => f.Type is not GenType.Function) && Random.Chance(60)) {
            GenField field = Random.Pick([.. structure.Fields.Where(f => f.Type is not GenType.Function)]);
            place = $"{target.Name}.{field.Name}";
            type = field.Type;
        }
        writer.Line($"{place} = {expressions.Expr(type, depth + 1, scope)}");
    }

    private void Compound(CodeWriter writer, GenScope scope, int depth, List<GenVariable> assignable) {
        GenVariable target = Random.Pick([.. assignable.Where(v => IsNumber(v.Type))]);
        string op = Random.Pick(["+=", "-=", "*="]);
        writer.Line($"{target.Name} {op} {expressions.Expr(target.Type, depth + 1, scope)}");
    }

    private void Increment(CodeWriter writer, List<GenVariable> assignable) {
        GenVariable target = Random.Pick([.. assignable.Where(v => v.Type == GenType.Int)]);
        writer.Line($"{target.Name}{(Random.Chance(70) ? "++" : "--")}");
    }

    private void FunctionBinding(CodeWriter writer, GenScope scope, int depth) {
        GenType.Function type = PickFunctionType();
        if (depth + 2 <= MaxStatementDepth && Random.Chance(40)) {
            BlockLambda(writer, scope, depth, type);
            return;
        }
        Binding(writer, scope, depth, type);
    }

    // A lambda with a block body: bindings, then the result as its last expression.
    private void BlockLambda(CodeWriter writer, GenScope scope, int depth, GenType.Function type) {
        string name = context.Fresh("f");
        scope.Push(FrameKind.Lambda);
        List<string> parameters = expressions.ParameterNames(type);
        writer.Open($"{name}: {type.Annotation} := ({string.Join(", ", parameters)}) => {{");
        for (int i = Random.Between(1, 2); i > 0; i--) {
            Binding(writer, scope, depth + 2);
        }
        writer.Line(expressions.Expr(type.Return, depth + 2, scope));
        writer.Close();
        scope.Pop();
        scope.Declare(new GenVariable(name, type, Assignable: false));
    }

    // An anonymous struct's fields are scalars: an array read back from one has lost its
    // element type, as measured, and a typed slot refuses it.
    private void AnonymousBinding(CodeWriter writer, GenScope scope, int depth) {
        var type = new GenType.Anonymous([.. Enumerable.Range(0, Random.Between(1, 3)).Select(i =>
            new GenField($"m{i}", PickScalar()))]);
        string name = context.Fresh("an");
        writer.Line($"{name} := {expressions.Expr(type, depth + 1, scope)}");
        scope.Declare(new GenVariable(name, type, Assignable: false));
    }

    private void Append(CodeWriter writer, GenScope scope, int depth) {
        GenVariable target = Random.Pick([.. scope.Assignable.Where(v => v.Type is GenType.ArrayOf)]);
        writer.Line($"{target.Name}.append({expressions.Expr(((GenType.ArrayOf)target.Type).Element, depth + 1, scope)})");
    }

    private void If(CodeWriter writer, GenScope scope, int depth) {
        writer.Open($"if ({expressions.Expr(GenType.Bool, depth + 1, scope)}) {{");
        Body(writer, scope, depth + 1, FrameKind.Block);
        if (Random.Chance(50)) {
            writer.Reopen("} else {");
            Body(writer, scope, depth + 1, FrameKind.Block);
        }
        writer.Close();
    }

    private void While(CodeWriter writer, GenScope scope, int depth) {
        string counter = context.Fresh("i");
        writer.Line($"{counter} := 0");
        scope.Declare(new GenVariable(counter, GenType.Int, Assignable: false));
        writer.Open($"while ({counter} < {Random.Between(1, 4).ToString(CultureInfo.InvariantCulture)}) {{");
        writer.Line($"{counter}++");
        Body(writer, scope, depth + 1, FrameKind.Loop);
        writer.Close();
    }

    private void ForRange(CodeWriter writer, GenScope scope, int depth) {
        string variable = context.Fresh("n");
        string step = Random.Chance(30) ? $" step {Random.Between(1, 3).ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        writer.Open($"for {variable} in 0..{Random.Between(0, 4).ToString(CultureInfo.InvariantCulture)}{step} {{");
        Loop(writer, scope, depth, [new GenVariable(variable, GenType.Int, Assignable: false)]);
    }

    private void ForArray(CodeWriter writer, GenScope scope, int depth) {
        if (Random.Chance(25)) {
            ForPipeline(writer, scope, depth);
            return;
        }
        var type = new GenType.ArrayOf(PickElementType());
        string element = context.Fresh("e");
        string iterable = expressions.Expr(type, depth + 1, scope);
        // A brace in a for header reads as the start of the loop's body, so a literal that holds
        // one is bound first.
        if (iterable.Contains('{')) {
            string bound = context.Fresh("xs");
            writer.Line($"{bound} := {iterable}");
            iterable = bound;
        }
        List<GenVariable> variables = [new(element, type.Element, Assignable: false)];
        if (Random.Chance(50)) {
            string index = context.Fresh("ix");
            variables.Insert(0, new GenVariable(index, GenType.Int, Assignable: false));
            writer.Open($"for {index}, {element} in {iterable} {{");
        } else {
            writer.Open($"for {element} in {iterable} {{");
        }
        Loop(writer, scope, depth, variables);
    }

    // A loop over a filter or a select: its element is typed unknown, as measured, so the body does not read it.
    private void ForPipeline(CodeWriter writer, GenScope scope, int depth) {
        string iterable = expressions.Pipeline(depth + 1, scope);
        if (iterable.Contains('{')) {
            string bound = context.Fresh("xs");
            writer.Line($"{bound} := {iterable}");
            iterable = bound;
        }
        writer.Open($"for {context.Fresh("e")} in {iterable} {{");
        Loop(writer, scope, depth, []);
    }

    private void ForMap(CodeWriter writer, GenScope scope, int depth) {
        GenVariable map = Random.Pick([.. scope.Readable.Where(v => v.Type is GenType.MapOf)]);
        string key = context.Fresh("key");
        string value = context.Fresh("val");
        writer.Open($"for {key}, {value} in {map.Name} {{");
        Loop(writer, scope, depth, [
            new GenVariable(key, GenType.String, Assignable: false),
            new GenVariable(value, ((GenType.MapOf)map.Type).Value, Assignable: false),
        ]);
    }

    private void Loop(CodeWriter writer, GenScope scope, int depth, IEnumerable<GenVariable> variables) {
        scope.Push(FrameKind.Loop);
        foreach (GenVariable variable in variables) scope.Declare(variable);
        Block(writer, scope, depth + 1, Random.Between(1, 3));
        scope.Pop();
        writer.Close();
    }

    private void Select(CodeWriter writer, GenScope scope, int depth) {
        bool onInt = Random.Chance(50);
        writer.Open($"select ({expressions.Expr(onInt ? GenType.Int : GenType.String, depth + 1, scope)}) {{");
        int cases = Random.Between(1, 3);
        for (int i = 0; i < cases; i++) {
            writer.Open($"case {(onInt ? (i * 2).ToString(CultureInfo.InvariantCulture) : $"\"c{i}\"")} {{");
            Body(writer, scope, depth + 1, FrameKind.Select);
            writer.Close();
        }
        if (Random.Chance(60)) {
            writer.Open("default {");
            Body(writer, scope, depth + 1, FrameKind.Select);
            writer.Close();
        }
        writer.Close();
    }

    private void Try(CodeWriter writer, GenScope scope, int depth) {
        writer.Open("try {");
        scope.Push(FrameKind.Try);
        Block(writer, scope, depth + 1, Random.Between(1, 3));
        if (Random.Chance(60)) {
            writer.Line($"throw {Random.Pick(ThrownErrors)} {{ message: {expressions.Expr(GenType.String, depth + 3, scope)} }}");
        }
        scope.Pop();
        bool catchAll = Random.Chance(50);
        int typed = catchAll ? Random.Between(0, 2) : Random.Between(1, 2);
        foreach (string error in ThrownErrors.Skip(Random.Next(ThrownErrors.Length - typed + 1)).Take(typed)) {
            Catch(writer, scope, depth, error);
        }
        if (catchAll) Catch(writer, scope, depth, null);
        if (Random.Chance(40)) {
            writer.Reopen("} finally {");
            Body(writer, scope, depth + 1, FrameKind.Finally);
        }
        writer.Close();
    }

    // A catch prints the error's message, so its binding is used, then runs a body of its own.
    private void Catch(CodeWriter writer, GenScope scope, int depth, string? error) {
        string binding = context.Fresh("err");
        writer.Reopen(error is null ? $"}} catch {binding} {{" : $"}} catch ({binding}: {error}) {{");
        writer.Line($"print({binding}.message)");
        Body(writer, scope, depth + 1, FrameKind.Try);
    }

    private void LoopExit(CodeWriter writer, GenScope scope, int depth) {
        writer.Open($"if ({expressions.Expr(GenType.Bool, depth + 1, scope)}) {{");
        writer.Line(Random.Chance(50) ? "break" : "continue");
        writer.Close();
    }

    private void EarlyReturn(CodeWriter writer, GenScope scope, int depth) {
        writer.Open($"if ({expressions.Expr(GenType.Bool, depth + 1, scope)}) {{");
        writer.Line($"return {expressions.Expr(scope.ReturnType!, depth + 2, scope)}");
        writer.Close();
    }

    private void Body(CodeWriter writer, GenScope scope, int depth, FrameKind kind) {
        scope.Push(kind);
        Block(writer, scope, depth, Random.Between(1, 3));
        scope.Pop();
    }

    private static bool IsNumber(GenType type) => type == GenType.Int || type == GenType.Float;
}
