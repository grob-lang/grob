namespace Grob.Torture.Generation;

/// <summary>
/// Writes an expression of a requested type from what is in scope (prompt §2 group 5). An
/// expression's depth is the nesting the generator bounds (<see cref="ProgramGenerator.MaxNesting"/>):
/// each statement, declaration and expression it sits inside, not counting parentheses, blocks and
/// the helper records an argument or an arm is held in. Every operator, ternary and switch is
/// written in parentheses, so one can stand anywhere another can.
/// </summary>
internal sealed class ExpressionGenerator(GenContext context) {
    private static readonly string[] Words = ["a", "ab", "grob", "xyz", "k0", "", "Hello"];
    private static readonly string[] FloatLiterals = ["0.5", "1.5", "2.25", "10.0", "0.0", "3.75"];
    private static readonly string[] MapKeys = ["k0", "k1", "k2"];
    private static readonly GenType[] Scalars = [GenType.Int, GenType.Float, GenType.String, GenType.Bool];

    private GenRandom Random => context.Random;

    /// <summary>The fewest nesting levels an expression of <paramref name="type"/> can take when no binding of it is in scope.</summary>
    public static int MinDepth(GenType type) => type switch {
        GenType.ArrayOf a => 1 + MinDepth(a.Element),
        GenType.MapOf m => 1 + MinDepth(m.Value),
        GenType.Function f => 1 + MinDepth(f.Return),
        GenType.Struct s => 1 + s.Fields.Max(f => MinDepth(f.Type)),
        GenType.Anonymous a => 1 + a.Fields.Max(f => MinDepth(f.Type)),
        _ => 1,
    };

    /// <summary>Whether a child of <paramref name="type"/> fits beneath an expression at <paramref name="depth"/>.</summary>
    public static bool Fits(GenType type, int depth) => MinDepth(type) <= ProgramGenerator.MaxNesting - depth;

    /// <summary>An expression of <paramref name="type"/> at <paramref name="depth"/>.</summary>
    public string Expr(GenType type, int depth, GenScope scope) {
        if (ProgramGenerator.MaxNesting - depth + 1 <= MinDepth(type)) return Minimal(type, depth, scope);
        if (Random.Chance(25 + (15 * depth))) return Leaf(type, depth, scope);
        List<Func<string>> forms = Forms(type, depth, scope);
        return forms.Count == 0 ? Leaf(type, depth, scope) : Random.Pick(forms)();
    }

    /// <summary>
    /// A lambda of <paramref name="type"/> whose body is an expression. In every position the
    /// generator writes a lambda (an annotated binding, a block body, a struct field, a function's
    /// argument, <c>filter</c> and <c>select</c>) the checker types its parameters <c>unknown</c>, as
    /// measured, which R-47 records; it refuses an <c>unknown</c> in a typed slot, so a parameter is
    /// never put in scope for the body. Half
    /// the time the body is the parameter itself or the parameter against a literal, as
    /// <c>x =&gt; x * 2</c> and <c>x =&gt; x &gt; 0</c> are: an unknown operand against a known
    /// one, the shape R-61 took.
    /// </summary>
    public string Lambda(GenType.Function type, int depth, GenScope scope) {
        scope.Push(FrameKind.Lambda);
        List<string> names = ParameterNames(type);
        string? onParameter = names.Count > 0 && Random.Chance(50) ? ParameterBody(names[0], type.Parameters[0], type.Return) : null;
        string body = onParameter ?? Expr(type.Return, depth + 1, scope);
        scope.Pop();
        return $"{ParameterList(names)} => {body}";
    }

    /// <summary>Fresh names for a lambda's parameters.</summary>
    public List<string> ParameterNames(GenType.Function type) => [.. type.Parameters.Select(_ => context.Fresh("p"))];

    /// <summary>A lambda's parameter list: a lone parameter bare, as <c>x =&gt;</c>, otherwise in parentheses.</summary>
    public static string ParameterList(List<string> names) => names.Count == 1 ? names[0] : $"({string.Join(", ", names)})";

    // A body over the parameter, of a shape that holds the result type the lambda promises for
    // the parameter's runtime type; null when the two types give no such shape.
    private string? ParameterBody(string parameter, GenType type, GenType result) {
        string literal = ScalarLiteral(type);
        return (type, result) switch {
            _ when type == result && Random.Chance(30) => parameter,
            _ when result == GenType.Bool && IsNumber(type) => $"({parameter} {Random.Pick([">", "<", "==", ">="])} {literal})",
            _ when result == GenType.Bool && type == GenType.String => $"({parameter} {Random.Pick(["==", "!="])} {literal})",
            _ when result == GenType.Bool && type == GenType.Bool => $"({parameter} == {literal})",
            _ when result == GenType.Int && type == GenType.Int => $"({parameter} {Random.Pick(["+", "-", "*"])} {literal})",
            _ when result == GenType.Float && IsNumber(type) => $"({parameter} {Random.Pick(["+", "*"])} {Random.Pick(FloatLiterals)})",
            _ when result == GenType.String && type == GenType.String => $"({parameter} + {literal})",
            _ when type == result => parameter,
            _ => null,
        };
    }

    private static bool IsNumber(GenType type) => type == GenType.Int || type == GenType.Float;

    /// <summary>A literal of a scalar type.</summary>
    public string ScalarLiteral(GenType type) => type switch {
        _ when type == GenType.Float => Random.Pick(FloatLiterals),
        _ when type == GenType.String => StringLiteral(),
        _ when type == GenType.Bool => Random.Chance(50) ? "true" : "false",
        _ => IntLiteral(),
    };

    /// <summary>A string literal with no escapes.</summary>
    public string StringLiteral() => $"\"{Random.Pick(Words)}\"";

    /// <summary>A small, non-negative integer literal, now and then a large one.</summary>
    public string IntLiteral() => Random.Weighted<string>([
        (90, Random.Between(0, 12).ToString(System.Globalization.CultureInfo.InvariantCulture)),
        (7, "1000000"),
        (3, "9223372036854775807"),
    ]);

    private string Minimal(GenType type, int depth, GenScope scope) {
        List<GenVariable> bindings = [.. scope.OfType(type)];
        if (bindings.Count > 0) return Random.Pick(bindings).Name;
        return type switch {
            GenType.Scalar => ScalarLiteral(type),
            GenType.Nullable => "nil",
            GenType.ArrayOf a => $"[{Minimal(a.Element, depth + 1, scope)}]",
            GenType.MapOf m => $"{m.Annotation}{{ \"{MapKeys[0]}\": {Minimal(m.Value, depth + 1, scope)} }}",
            GenType.Struct s => Construction(s, s.Fields.Select(f => Minimal(f.Type, depth + 1, scope))),
            GenType.Anonymous a => AnonymousLiteral(a, a.Fields.Select(f => Minimal(f.Type, depth + 1, scope))),
            GenType.Function f => MinimalLambda(f, depth, scope),
            _ => throw new InvalidOperationException($"no literal for {type}"),
        };
    }

    private string MinimalLambda(GenType.Function type, int depth, GenScope scope) {
        scope.Push(FrameKind.Lambda);
        string parameters = ParameterList(ParameterNames(type));
        string body = Minimal(type.Return, depth + 1, scope);
        scope.Pop();
        return $"{parameters} => {body}";
    }

    private string Leaf(GenType type, int depth, GenScope scope) {
        List<GenVariable> bindings = [.. scope.OfType(type)];
        if (bindings.Count > 0 && Random.Chance(70)) return Random.Pick(bindings).Name;
        return type switch {
            GenType.Scalar => ScalarLiteral(type),
            GenType.Nullable n => Random.Chance(40) ? "nil" : Expr(n.Inner, depth, scope),
            GenType.ArrayOf a => ArrayLiteral(a, depth, scope),
            GenType.MapOf m => MapLiteral(m, depth, scope),
            GenType.Struct s => Construction(s, s.Fields.Select(f => Expr(f.Type, depth + 1, scope))),
            GenType.Anonymous a => AnonymousLiteral(a, a.Fields.Select(f => Expr(f.Type, depth + 1, scope))),
            GenType.Function f => Lambda(f, depth, scope),
            _ => throw new InvalidOperationException($"no literal for {type}"),
        };
    }

    private string ArrayLiteral(GenType.ArrayOf type, int depth, GenScope scope) =>
        $"[{string.Join(", ", Enumerable.Range(0, Random.Between(1, 3)).Select(_ => Expr(type.Element, depth + 1, scope)))}]";

    private string MapLiteral(GenType.MapOf type, int depth, GenScope scope) {
        IEnumerable<string> entries = MapKeys.Take(Random.Between(1, MapKeys.Length))
            .Select(k => $"\"{k}\": {Expr(type.Value, depth + 1, scope)}");
        return $"{type.Annotation}{{ {string.Join(", ", entries)} }}";
    }

    private static string Construction(GenType.Struct type, IEnumerable<string> values) =>
        $"{type.Name} {{ {string.Join(", ", type.Fields.Zip(values, (f, v) => $"{f.Name}: {v}"))} }}";

    private static string AnonymousLiteral(GenType.Anonymous type, IEnumerable<string> values) =>
        $"#{{ {string.Join(", ", type.Fields.Zip(values, (f, v) => $"{f.Name}: {v}"))} }}";

    private List<Func<string>> Forms(GenType type, int depth, GenScope scope) {
        var forms = new List<Func<string>>();
        switch (type) {
            case GenType.Scalar:
                AddScalarForms(forms, type, depth, scope);
                break;
            case GenType.ArrayOf array:
                AddArrayForms(forms, array, depth, scope);
                break;
            case GenType.Nullable nullable:
                AddNullableForms(forms, nullable, depth, scope);
                break;
        }
        if (type is not GenType.Anonymous) AddGenericForms(forms, type, depth, scope);
        return forms;
    }

    private void AddScalarForms(List<Func<string>> forms, GenType type, int depth, GenScope scope) {
        if (type == GenType.Int) AddIntForms(forms, depth, scope);
        else if (type == GenType.Float) AddFloatForms(forms, depth, scope);
        else if (type == GenType.String) AddStringForms(forms, depth, scope);
        else AddBoolForms(forms, depth, scope);

        foreach (GenNative native in context.Members.Returning(type).Where(n => NativeFits(n, depth))) {
            forms.Add(() => NativeCall(native, depth, scope));
        }
        forms.Add(() => $"({Expr(new GenType.Nullable(type), depth + 1, scope)} ?? {Expr(type, depth + 1, scope)})");
        foreach ((GenVariable holder, GenField field) in NullableStructFields(scope, type)) {
            forms.Add(() => $"({holder.Name}?.{field.Name} ?? {Expr(type, depth + 1, scope)})");
        }
    }

    private void AddIntForms(List<Func<string>> forms, int depth, GenScope scope) {
        foreach (string op in new[] { "+", "-", "*" }) {
            forms.Add(() => Binary(op, GenType.Int, GenType.Int, depth, scope));
        }
        foreach (string op in new[] { "/", "%" }) {
            forms.Add(() => $"({Expr(GenType.Int, depth + 1, scope)} {op} {Divisor(depth, scope)})");
        }
        forms.Add(() => $"(-{Expr(GenType.Int, depth + 1, scope)})");
        if (depth + 4 <= ProgramGenerator.MaxNesting) forms.Add(() => $"{Pipeline(depth + 1, scope)}.length");
        foreach (GenType sized in SizedTypes(scope).Where(t => Fits(t, depth))) {
            forms.Add(() => $"{Receiver(sized, depth + 1, scope)}.length");
        }
    }

    private void AddFloatForms(List<Func<string>> forms, int depth, GenScope scope) {
        foreach (string op in new[] { "+", "-", "*", "/" }) {
            forms.Add(() => Random.Chance(50)
                ? Binary(op, GenType.Float, Random.Chance(50) ? GenType.Int : GenType.Float, depth, scope)
                : Binary(op, GenType.Int, GenType.Float, depth, scope));
        }
    }

    private void AddStringForms(List<Func<string>> forms, int depth, GenScope scope) {
        forms.Add(() => Binary("+", GenType.String, GenType.String, depth, scope));
        forms.Add(() => Interpolation(scope));
    }

    private void AddBoolForms(List<Func<string>> forms, int depth, GenScope scope) {
        forms.Add(() => Comparison(depth, scope));
        forms.Add(() => Binary(Random.Chance(50) ? "&&" : "||", GenType.Bool, GenType.Bool, depth, scope));
        forms.Add(() => $"(!{Expr(GenType.Bool, depth + 1, scope)})");

        foreach (GenType.ArrayOf array in ArrayTypesInScope(scope).Where(a => a.Element is GenType.Scalar && depth + 2 <= ProgramGenerator.MaxNesting)) {
            forms.Add(() => $"{Receiver(array, depth + 2, scope)}.contains({Expr(array.Element, depth + 1, scope)})");
        }
        foreach (GenVariable map in scope.Readable.Where(v => v.Type is GenType.MapOf && depth + 2 <= ProgramGenerator.MaxNesting)) {
            forms.Add(() => $"{map.Name}.contains(\"{Random.Pick(MapKeys)}\")");
        }
        foreach (GenVariable nullable in scope.Readable.Where(v => v.Type is GenType.Nullable)) {
            forms.Add(() => $"({nullable.Name} {(Random.Chance(50) ? "==" : "!=")} nil)");
        }
    }

    private void AddArrayForms(List<Func<string>> forms, GenType.ArrayOf type, int depth, GenScope scope) {
        if (depth + 2 > ProgramGenerator.MaxNesting) return;
        foreach (GenVariable map in scope.Readable.Where(v => v.Type is GenType.MapOf m && m.Value == type.Element)) {
            forms.Add(() => $"{map.Name}.values");
        }
        if (type.Element == GenType.String) {
            forms.Add(() => $"{Receiver(GenType.String, depth + 2, scope)}.split({StringLiteral()})");
            foreach (GenVariable map in scope.Readable.Where(v => v.Type is GenType.MapOf)) {
                forms.Add(() => $"{map.Name}.keys");
            }
        }
    }

    private void AddNullableForms(List<Func<string>> forms, GenType.Nullable type, int depth, GenScope scope) {
        forms.Add(() => "nil");
        forms.Add(() => Expr(type.Inner, depth, scope));
        var array = new GenType.ArrayOf(type.Inner);
        if (depth + 2 <= ProgramGenerator.MaxNesting && Fits(array, depth + 1)) {
            forms.Add(() => $"{Receiver(array, depth + 2, scope)}.{(Random.Chance(50) ? "first" : "last")}()");
        }
        foreach (GenVariable map in scope.Readable.Where(v => v.Type is GenType.MapOf m && m.Value == type.Inner)) {
            forms.Add(() => $"{map.Name}[\"{Random.Pick(MapKeys)}\"]");
        }
        foreach ((GenVariable holder, GenField field) in NullableStructFields(scope, type.Inner)) {
            forms.Add(() => $"{holder.Name}?.{field.Name}");
        }
        foreach (GenNative native in context.Members.Returning(type).Where(n => NativeFits(n, depth))) {
            forms.Add(() => NativeCall(native, depth, scope));
        }
    }

    private void AddGenericForms(List<Func<string>> forms, GenType type, int depth, GenScope scope) {
        bool fits = Fits(type, depth);
        // The checker refuses a ternary or a switch whose arms mix nil with a value, so neither is
        // written for a nullable type.
        // Nor for an array, a map or a struct: the checker judges an array of an annotated int
        // binding and one of an inferred int binding to differ, and a struct out of a switch loses
        // its type's name (defects for the campaign's triage).
        if (fits && type is GenType.Scalar) {
            forms.Add(() => $"({Expr(GenType.Bool, depth + 1, scope)} ? {Expr(type, depth + 1, scope)} : {Expr(type, depth + 1, scope)})");
            forms.Add(() => Switch(type, depth, scope));
        }
        foreach (GenFunction function in context.Functions.Take(scope.VisibleFunctions).Where(f => f.Type.Return == type)) {
            forms.Add(() => $"{function.Name}({Arguments(function.Type.Parameters, depth, scope)})");
        }
        foreach (GenVariable callee in scope.Readable.Where(v => v.Type is GenType.Function f && f.Return == type)) {
            forms.Add(() => $"{callee.Name}({Arguments(((GenType.Function)callee.Type).Parameters, depth, scope)})");
        }
        foreach ((GenVariable holder, GenField field) in StructFields(scope)) {
            // An array read from a field has lost its element type, so its elements meet a typed
            // slot as unknown; array fields are built and passed but not read back.
            if (field.Type == type && field.Type is not GenType.ArrayOf) forms.Add(() => $"{holder.Name}.{field.Name}");
            if (field.Type is GenType.Function f && f.Return == type && depth + 2 <= ProgramGenerator.MaxNesting) {
                forms.Add(() => $"{holder.Name}.{field.Name}({Arguments(f.Parameters, depth, scope)})");
            }
        }
        foreach (GenVariable array in scope.Readable.Where(v => v.Type is GenType.ArrayOf a && a.Element == type)) {
            forms.Add(() => $"{array.Name}[{(Random.Chance(80) ? Random.Between(0, 2).ToString(System.Globalization.CultureInfo.InvariantCulture) : Expr(GenType.Int, depth + 1, scope))}]");
        }
    }

    /// <summary>
    /// A <c>filter</c> or <c>select</c> over an array of a scalar. The checker types the result's
    /// elements <c>unknown</c> and refuses one in a typed slot, so a pipeline is read only through
    /// <c>length</c> or as a loop that does not read its element.
    /// </summary>
    public string Pipeline(int depth, GenScope scope) {
        var source = new GenType.ArrayOf(Random.Pick(Scalars));
        GenType result = Random.Chance(50) ? GenType.Bool : Random.Pick(Scalars);
        string member = result == GenType.Bool && Random.Chance(70) ? "filter" : "select";
        var function = new GenType.Function([source.Element], result);
        return $"{Receiver(source, depth + 2, scope)}.{member}({Lambda(function, depth + 1, scope)})";
    }

    private string Switch(GenType type, int depth, GenScope scope) {
        bool onInt = Random.Chance(60);
        string subject = Expr(onInt ? GenType.Int : GenType.String, depth + 1, scope);
        int count = Random.Between(1, 3);
        IEnumerable<string> patterns = onInt
            ? Enumerable.Range(0, count).Select(i => (i * 3).ToString(System.Globalization.CultureInfo.InvariantCulture))
            : Words.Skip(Random.Next(Words.Length - count)).Take(count).Select(w => $"\"{w}\"");
        List<string> arms = [.. patterns.Select(p => $"{p} => {Expr(type, depth + 1, scope)}")];
        if (onInt && Random.Chance(40)) arms.Add($">= {count * 3} => {Expr(type, depth + 1, scope)}");
        arms.Add($"_ => {Expr(type, depth + 1, scope)}");
        return $"({subject} switch {{ {string.Join(", ", arms)} }})";
    }

    private string Comparison(int depth, GenScope scope) {
        GenType operand = Random.Pick(Scalars);
        string[] operators = operand == GenType.Bool ? ["==", "!="]
            : operand == GenType.String ? ["==", "!=", "<", ">"]
            : ["<", "<=", ">", ">=", "==", "!="];
        return Binary(Random.Pick(operators), operand, operand, depth, scope);
    }

    private string Binary(string op, GenType left, GenType right, int depth, GenScope scope) =>
        $"({Expr(left, depth + 1, scope)} {op} {Expr(right, depth + 1, scope)})";

    private string Divisor(int depth, GenScope scope) =>
        Random.Chance(85) ? Random.Between(1, 9).ToString(System.Globalization.CultureInfo.InvariantCulture) : Expr(GenType.Int, depth + 1, scope);

    // An interpolation's holes are bindings or integer literals: a string literal inside one
    // would nest quotes, which is the lexer's question, not the checker's.
    private string Interpolation(GenScope scope) {
        List<GenVariable> holes = [.. scope.Readable.Where(v => v.Type is GenType.Scalar)];
        IEnumerable<string> parts = Enumerable.Range(0, Random.Between(1, 2)).Select(_ =>
            $"{Random.Pick(Words)} ${{{(holes.Count > 0 && Random.Chance(80) ? Random.Pick(holes).Name : IntLiteral())}}}");
        return $"\"{string.Join(" ", parts)}\"";
    }

    private string Arguments(IReadOnlyList<GenType> parameters, int depth, GenScope scope) =>
        string.Join(", ", parameters.Select(p => Expr(p, depth + 1, scope)));

    // A native's integer argument is a small literal: a count or a width drawn from arithmetic
    // could ask for gigabytes of string.
    private string NativeCall(GenNative native, int depth, GenScope scope) {
        string arguments = string.Join(", ", native.Parameters.Select(p =>
            p == GenType.Int ? Random.Between(0, 9).ToString(System.Globalization.CultureInfo.InvariantCulture) : Expr(p, depth + 1, scope)));
        if (native.Owner is null) return $"{native.Name}({arguments})";
        return native.IsProperty
            ? $"{Receiver(native.Owner, depth + 1, scope)}.{native.Name}"
            : $"{Receiver(native.Owner, depth + 2, scope)}.{native.Name}({arguments})";
    }

    private static bool NativeFits(GenNative native, int depth) =>
        depth + (native.Owner is not null && !native.IsProperty ? 2 : 1) <= ProgramGenerator.MaxNesting
        && native.Parameters.All(p => Fits(p, depth));

    // A receiver that starts with a digit is parenthesised, so 3.toString() cannot lex as a float.
    private string Receiver(GenType type, int depth, GenScope scope) {
        string receiver = Expr(type, depth, scope);
        return char.IsAsciiDigit(receiver[0]) ? $"({receiver})" : receiver;
    }

    private static IEnumerable<GenType> SizedTypes(GenScope scope) =>
        new GenType[] { GenType.String, new GenType.ArrayOf(GenType.Int) }
            .Concat(scope.Readable.Select(v => v.Type).Where(t => t is GenType.ArrayOf or GenType.MapOf).Distinct());

    private static IEnumerable<GenType.ArrayOf> ArrayTypesInScope(GenScope scope) =>
        new[] { new GenType.ArrayOf(GenType.Int), new GenType.ArrayOf(GenType.String) }
            .Concat(scope.Readable.Select(v => v.Type).OfType<GenType.ArrayOf>()).Distinct();

    private static IEnumerable<(GenVariable Holder, GenField Field)> StructFields(GenScope scope) =>
        scope.Readable.SelectMany(v => FieldsOf(v.Type).Select(f => (v, f)));

    private static IEnumerable<(GenVariable Holder, GenField Field)> NullableStructFields(GenScope scope, GenType fieldType) =>
        scope.Readable
            .Where(v => v.Type is GenType.Nullable { Inner: GenType.Struct })
            .SelectMany(v => FieldsOf(((GenType.Nullable)v.Type).Inner).Where(f => f.Type == fieldType && f.Type is not (GenType.Nullable or GenType.Function)).Select(f => (v, f)));

    private static IReadOnlyList<GenField> FieldsOf(GenType type) => type switch {
        GenType.Struct s => s.Fields,
        GenType.Anonymous a => a.Fields,
        _ => [],
    };
}
