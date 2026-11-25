using System.Globalization;

namespace Grob.Torture.Generation;

/// <summary>
/// The generator (prompt §2 group 5): a depth-bounded, type-directed writer of programs over the
/// v1 surface, meant to type-check. A program is written in the order a script takes: a
/// <c>param</c>, struct types, functions, then top-level code that prints what it computes, so the
/// VM has work to do.
/// </summary>
/// <remarks>
/// A program depends on its campaign seed and index alone, so a seed's programs never change when
/// another is drawn, as the CI slice's mutants do not (decision T). Nesting is bounded at
/// <see cref="MaxNesting"/>, which keeps a stack overflow (F-3) out of the test host (gate report
/// §4.4). R-40's benchmark generator is a separate concern.
/// </remarks>
internal sealed class ProgramGenerator {
    /// <summary>The origin every generated seed carries.</summary>
    public const string Origin = "generated";

    /// <summary>
    /// The deepest nesting of statements, declarations and expressions a program reaches, not
    /// counting blocks, parentheses and expression statements (gate report §4.4: 8 or less).
    /// </summary>
    public const int MaxNesting = 8;

    private readonly GenContext _context;
    private readonly ExpressionGenerator _expressions;
    private readonly StatementGenerator _statements;
    private readonly CodeWriter _writer = new();

    private ProgramGenerator(GenContext context) {
        _context = context;
        _expressions = new ExpressionGenerator(context);
        _statements = new StatementGenerator(context, _expressions);
    }

    private GenRandom Random => _context.Random;

    /// <summary>The program at <paramref name="index"/> of the campaign <paramref name="campaignSeed"/>.</summary>
    public static Seed Generate(ulong campaignSeed, int index) {
        string seedText = campaignSeed.ToString("x16", CultureInfo.InvariantCulture);
        var random = new GenRandom(SeedCorpus.Fnv1a64($"{seedText}|gen|{index}"));
        var generator = new ProgramGenerator(new GenContext(random, MemberTable.Stdlib));
        return new Seed($"generated/{seedText}/{index}", SeedKind.Generated, Origin, generator.Write());
    }

    private string Write() {
        GenVariable? param = Random.Chance(25) ? Param() : null;
        for (int i = Random.Between(0, 3); i > 0; i--) {
            StructType();
        }
        for (int i = Random.Between(0, 3); i > 0; i--) {
            Function();
        }

        var scope = new GenScope(returnType: null, visibleFunctions: _context.Functions.Count);
        if (param is not null) scope.Declare(param);
        if (Random.Chance(30)) Const(scope);
        if (Random.Chance(30)) Readonly(scope);
        _statements.Block(_writer, scope, depth: 1, count: Random.Between(5, 14));
        foreach (GenVariable shown in scope.Readable.Where(v => v.Type is GenType.Scalar).TakeLast(2)) {
            _writer.Line($"print({shown.Name})");
        }
        return _writer.ToString();
    }

    private GenVariable Param() {
        string name = _context.Fresh("arg");
        bool isInt = Random.Chance(60);
        GenType type = isInt ? GenType.Int : GenType.String;
        _writer.Line($"param {name}: {type.Annotation} = {_expressions.ScalarLiteral(type)}");
        return new GenVariable(name, type, Assignable: false);
    }

    // A field may be a struct declared earlier only when that struct holds nothing deeper than a
    // scalar, so an array of the outer struct still fits the room a statement leaves its expressions.
    private void StructType() {
        string name = _context.Fresh("T");
        List<GenType.Struct> flat = [.. _context.Structs.Where(s => ExpressionGenerator.MinDepth(s) == 2)];
        List<GenField> fields = [.. Enumerable.Range(0, Random.Between(1, 3)).Select(i => new GenField($"f{i}", Random.Weighted<Func<GenType>>([
            (50, _statements.PickScalar),
            (15, () => new GenType.Nullable(_statements.PickScalar())),
            (15, () => new GenType.ArrayOf(_statements.PickScalar())),
            (flat.Count > 0 ? 10 : 0, () => Random.Pick(flat)),
            (10, () => new GenType.Function([_statements.PickScalar()], _statements.PickScalar())),
        ])()))];
        _writer.Open($"type {name} {{");
        foreach (GenField field in fields) {
            _writer.Line($"{field.Name}: {field.Type.Annotation}");
        }
        _writer.Close();
        _context.Structs.Add(new GenType.Struct(name, fields));
    }

    // A function's body may call only the functions declared before it, so none can reach itself.
    private void Function() {
        string name = _context.Fresh("g");
        var type = new GenType.Function(
            [.. Enumerable.Range(0, Random.Between(0, 3)).Select(_ => _statements.PickType(allowFunction: true))],
            _statements.PickType(allowFunction: Random.Chance(30)));
        var scope = new GenScope(type.Return, _context.Functions.Count);
        List<string> parameters = [.. type.Parameters.Select(p => {
            string parameter = _context.Fresh("a");
            scope.Declare(new GenVariable(parameter, p, Assignable: false));
            return $"{parameter}: {p.Annotation}";
        })];

        _writer.Open($"fn {name}({string.Join(", ", parameters)}): {type.Return.Annotation} {{");
        _statements.Block(_writer, scope, depth: 2, count: Random.Between(1, 4));
        _writer.Line($"return {_expressions.Expr(type.Return, depth: 3, scope)}");
        _writer.Close();
        _context.Functions.Add(new GenFunction(name, type));
    }

    // A constant's operands are small: folding one that overflows crashes the compiler (R-68),
    // which is the oracle's to find through the mutator's const-edge operator, not this test's.
    private void Const(GenScope scope) {
        string name = _context.Fresh("K");
        string op = Random.Pick(["+", "-", "*", "/"]);
        _writer.Line($"const {name} := {Random.Between(0, 12).ToString(CultureInfo.InvariantCulture)} {op} {Random.Between(1, 9).ToString(CultureInfo.InvariantCulture)}");
        scope.Declare(new GenVariable(name, GenType.Int, Assignable: false));
    }

    private void Readonly(GenScope scope) {
        string name = _context.Fresh("r");
        GenType type = _statements.PickScalar();
        _writer.Line($"readonly {name} := {_expressions.Expr(type, depth: 2, scope)}");
        scope.Declare(new GenVariable(name, type, Assignable: false));
    }
}
