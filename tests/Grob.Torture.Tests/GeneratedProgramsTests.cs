using Grob.Compiler;
using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;
using Grob.Runtime;
using Grob.Stdlib;
using Grob.Torture.Fakes;
using Grob.Torture.Generation;
using Grob.Torture.Mutation;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Torture.Tests;

/// <summary>
/// Properties of a fixed sample of generated programs (prompt §2 group 5, §4): each type-checks
/// and compiles, together they reach the v1 surface the prompt names, none nests past the bound
/// that keeps a stack overflow out of the test host (gate report §4.4), and each ends on its own.
/// </summary>
public sealed class GeneratedProgramsTests {
    private const ulong CampaignSeed = 20261124UL;
    private const int SampleSize = 300;

    private static readonly Lazy<IReadOnlyList<Seed>> Sample = new(() =>
        [.. Enumerable.Range(0, SampleSize).Select(i => ProgramGenerator.Generate(CampaignSeed, i))]);

    [Fact]
    public void Sample_EveryProgram_TypeChecksAndCompilesWithoutErrors() {
        var failures = new List<(Seed Program, IReadOnlyList<Diagnostic> Errors)>();
        foreach (Seed program in Sample.Value) {
            IReadOnlyList<Diagnostic> errors = Compile(program.Source);
            if (errors.Count > 0) failures.Add((program, errors));
        }

        // One example of each distinct error, so a failing run says which shapes the generator
        // gets wrong, not only how many programs.
        IEnumerable<string> shapes = failures
            .SelectMany(f => f.Errors.Select(e => (Shape: $"{e.Code} {MessageNormaliser.Normalise(e.Message)}", f.Program, Error: e)))
            .GroupBy(e => e.Shape)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Count(),4} x {g.Key}\n       first: {g.First().Program.Id} line {g.First().Error.Range.Start.Line}: {g.First().Error.Message}\n" +
                $"         {Line(g.First().Program.Source, g.First().Error.Range.Start.Line)}");
        Assert.True(failures.Count == 0,
            $"{failures.Count} of {SampleSize} generated programs did not type-check:\n" + string.Join("\n", shapes) +
            (failures.Count > 0 ? $"\n\n{failures[0].Program.Id}\n{Numbered(failures[0].Program.Source)}" : ""));
    }

    public static TheoryData<string> Constructs => [.. ConstructCensus.Keys];

    [Theory]
    [MemberData(nameof(Constructs))]
    public void Sample_EveryConstructOfTheV1Surface_AppearsAtLeastOnce(string construct) {
        int count = Sample.Value.Sum(p => AstNodes.Enumerate(Parse(p.Source)).Count(e => ConstructCensus[construct](e.Node)));

        Assert.True(count > 0, $"no generated program in the sample holds {construct}");
    }

    [Fact]
    public void Sample_DeepestNesting_IsWithinTheGeneratorsBound() {
        (string Id, int Depth) deepest = Sample.Value
            .Select(p => (p.Id, Depth: NestingDepth(Parse(p.Source))))
            .MaxBy(d => d.Depth);

        Assert.True(deepest.Depth <= ProgramGenerator.MaxNesting,
            $"{deepest.Id} nests {deepest.Depth} deep, past the bound of {ProgramGenerator.MaxNesting}");
        Assert.True(deepest.Depth >= ProgramGenerator.MaxNesting - 2, $"the deepest program nests only {deepest.Depth} deep");
    }

    [Fact]
    public void Sample_EveryProgram_RunsWithoutStallingItsWorker() {
        // The watchdog only, a safety limit. Whether a program ends is read from its text by the
        // three tests below, not from the VM's time budget, which a slow runner can spend on a
        // program that does end (gate report §4.4). A run's findings are not asserted: they are
        // defects in Grob, which the campaign triages (prompt §2 group 6), and generated programs
        // are kept out of the CI slice until then (plan decision D1).
        var oracle = new Oracle(OracleBudget.Ci);
        foreach (Seed program in Sample.Value) {
            if (oracle.Run(program.Source, program.Origin).TimedOut) {
                Assert.Fail($"{program.Id} stalled its worker:\n{program.Source}");
            }
        }
    }

    // The generator's termination argument (StatementGenerator's summary), checked in every
    // program's text: a while loop counts up to a literal from a counter only its first statement
    // bumps, a function calls no function declared at or after it, and no loop body grows an array.

    [Fact]
    public void Sample_EveryWhile_CountsUpFromACounterOnlyItsFirstStatementBumps() {
        var failures = new List<string>();
        foreach (Seed program in Sample.Value) {
            List<object> nodes = [.. AstNodes.Enumerate(Parse(program.Source)).Select(e => e.Node)];
            failures.AddRange(nodes.OfType<WhileStmt>()
                .Where(loop => !CountsUp(loop, nodes))
                .Select(loop => $"{program.Id} line {loop.Range.Start.Line}"));
        }

        Assert.True(failures.Count == 0, "while loops their counter does not bound: " + string.Join(", ", failures));
    }

    [Fact]
    public void Sample_EveryFunction_CallsOnlyFunctionsDeclaredBeforeIt() {
        var failures = new List<string>();
        foreach (Seed program in Sample.Value) {
            List<FnDecl> functions = [.. AstNodes.Enumerate(Parse(program.Source)).Select(e => e.Node).OfType<FnDecl>()
                .OrderBy(f => f.Range.Start.Line)];
            for (int i = 0; i < functions.Count; i++) {
                HashSet<string> notYetCallable = [.. functions.Skip(i).Select(f => f.Name)];
                failures.AddRange(AstNodes.Enumerate(functions[i].Body).Select(e => e.Node).OfType<CallExpr>()
                    .Where(call => call.Callee is IdentifierExpr { Name: var callee } && notYetCallable.Contains(callee))
                    .Select(call => $"{program.Id}: {functions[i].Name} calls {((IdentifierExpr)call.Callee).Name} on line {call.Range.Start.Line}"));
            }
        }

        Assert.True(failures.Count == 0, "calls that could recurse: " + string.Join(", ", failures));
    }

    [Fact]
    public void Sample_NoLoopBody_AppendsToAnArray() {
        var failures = new List<string>();
        foreach (Seed program in Sample.Value) {
            IReadOnlyList<AstEntry> entries = AstNodes.Enumerate(Parse(program.Source));
            Dictionary<object, object?> parents = entries.ToDictionary(e => e.Node, e => e.Parent, ReferenceEqualityComparer.Instance);
            failures.AddRange(entries.Select(e => e.Node).OfType<CallExpr>()
                .Where(call => call.Callee is MemberAccessExpr { Member: "append" } && InsideALoop(call, parents))
                .Select(call => $"{program.Id} line {call.Range.Start.Line}"));
        }

        Assert.True(failures.Count == 0, "appends inside a loop: " + string.Join(", ", failures));
    }

    private static bool CountsUp(WhileStmt loop, List<object> nodes) {
        if (Unwrapped(loop.Condition) is not BinaryExpr { Operator: BinaryOperator.Less, Left: IdentifierExpr { Name: var counter }, Right: IntLiteralExpr }) {
            return false;
        }
        if (loop.Body.Statements is not [IncrementStmt { Kind: IncrementKind.Increment, Target: IdentifierExpr { Name: var bumped } } first, ..] || bumped != counter) {
            return false;
        }
        return nodes.TrueForAll(node => ReferenceEquals(node, first) || WrittenName(node) != counter);
    }

    private static Expression Unwrapped(Expression expression) =>
        expression is GroupingExpr grouping ? Unwrapped(grouping.Inner) : expression;

    private static string? WrittenName(object node) => node switch {
        AssignmentStmt { Target: IdentifierExpr target } => target.Name,
        CompoundAssignmentStmt { Target: IdentifierExpr target } => target.Name,
        IncrementStmt { Target: IdentifierExpr target } => target.Name,
        _ => null,
    };

    private static bool InsideALoop(object node, Dictionary<object, object?> parents) {
        for (object? current = parents[node]; current is not null; current = parents[current]) {
            if (current is WhileStmt or ForInStmt) return true;
        }
        return false;
    }

    // A string method no array or map shares a name with, so an array's call cannot stand in for one.
    private static readonly HashSet<string> StringOnlyMethods = [.. MemberTable.Stdlib.Natives
        .Where(n => n.Owner == GenType.String && !n.IsProperty)
        .Select(n => n.Name)
        .Except(["contains", "toString"])];

    // The generator names a struct's fields f0, f1, … and an anonymous struct's m0, m1, …, and its
    // loop variables by kind (e for an element, ix for an index, key for a map's key), so the
    // census reads what a node is from its names without running the checker.
    private static bool IsFieldName(string member) =>
        member.Length > 1 && member[0] is 'f' or 'm' && member[1..].All(char.IsAsciiDigit);

    // One predicate per construct the prompt names, and the shapes beside them the generator
    // owes the checker: a construct missing from every program in the sample fails its case.
    private static readonly Dictionary<string, Func<object, bool>> ConstructCensus = new(StringComparer.Ordinal) {
        ["binding"] = n => n is VarDeclStmt,
        ["annotated binding"] = n => n is VarDeclStmt { AnnotatedType: not null },
        ["const"] = n => n is ConstDecl,
        ["readonly"] = n => n is ReadonlyDecl,
        ["assignment"] = n => n is AssignmentStmt,
        ["compound assignment"] = n => n is CompoundAssignmentStmt,
        ["increment"] = n => n is IncrementStmt,
        ["arithmetic"] = n => n is BinaryExpr { Operator: BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply },
        ["division"] = n => n is BinaryExpr { Operator: BinaryOperator.Divide or BinaryOperator.Modulo },
        ["comparison"] = n => n is BinaryExpr { Operator: BinaryOperator.Less or BinaryOperator.GreaterEqual or BinaryOperator.Equal },
        ["logic"] = n => n is BinaryExpr { Operator: BinaryOperator.And or BinaryOperator.Or },
        ["unary"] = n => n is UnaryExpr,
        ["ternary"] = n => n is TernaryExpr,
        ["float literal"] = n => n is FloatLiteralExpr,
        ["interpolated string"] = n => n is InterpolatedStringExpr,
        ["string method"] = n => n is CallExpr { Callee: MemberAccessExpr { Member: var member } } && StringOnlyMethods.Contains(member),
        ["array literal"] = n => n is ArrayLiteralExpr,
        ["index"] = n => n is IndexExpr,
        ["map literal"] = n => n is MapLiteralExpr,
        ["struct type"] = n => n is TypeDecl,
        ["struct construction"] = n => n is StructConstructionExpr,
        ["anonymous struct"] = n => n is AnonStructExpr,
        ["field read"] = n => n is MemberAccessExpr { IsOptional: false, Member: var member } && IsFieldName(member),
        ["function"] = n => n is FnDecl,
        ["return"] = n => n is ReturnStmt,
        ["function type"] = n => n is FunctionTypeRef,
        ["lambda"] = n => n is LambdaExpr { Body: LambdaExpressionBody },
        ["block lambda"] = n => n is LambdaExpr { Body: LambdaBlockBody },
        ["nullable type"] = n => n is TypeRef { IsNullable: true },
        ["nil"] = n => n is NilLiteralExpr,
        ["nil coalescing"] = n => n is BinaryExpr { Operator: BinaryOperator.NilCoalesce },
        ["optional chaining"] = n => n is MemberAccessExpr { IsOptional: true },
        ["if"] = n => n is IfStmt,
        ["else"] = n => n is IfStmt { Else: not null },
        ["while"] = n => n is WhileStmt,
        ["break"] = n => n is BreakStmt,
        ["continue"] = n => n is ContinueStmt,
        ["for over a range"] = n => n is ForInStmt { Iterable: NumericRangeExpr },
        ["for over an array"] = n => n is ForInStmt { Variables: [var element] } && element.StartsWith('e'),
        ["for over an array with its index"] = n => n is ForInStmt { Variables: [var index, _] } && index.StartsWith("ix", StringComparison.Ordinal),
        ["for over a map"] = n => n is ForInStmt { Variables: [var key, _] } && key.StartsWith("key", StringComparison.Ordinal),
        ["select"] = n => n is SelectStmt,
        ["select default"] = n => n is SelectStmt { Default: not null },
        ["switch expression"] = n => n is SwitchExprNode,
        ["relational arm"] = n => n is SwitchArm { Pattern: RelationalPattern },
        ["try"] = n => n is TryStmt,
        ["typed catch"] = n => n is CatchClause { ExceptionType: not null },
        ["catch-all"] = n => n is CatchClause { ExceptionType: null },
        ["finally"] = n => n is TryStmt { Finally: not null },
        ["throw"] = n => n is ThrowStmt,
        ["param"] = n => n is ParamDecl,
    };

    // The nesting the generator bounds: each statement, declaration and expression a node sits
    // inside, not counting the wrappers that hold no recursion of their own (a block, an
    // expression statement, parentheses, a call argument, a lambda's body).
    private static int NestingDepth(CompilationUnit unit) {
        IReadOnlyList<AstEntry> entries = AstNodes.Enumerate(unit);
        Dictionary<object, object?> parents = entries.ToDictionary(e => e.Node, e => e.Parent, ReferenceEqualityComparer.Instance);
        return entries.Max(e => Depth(e.Node, parents));
    }

    private static int Depth(object node, Dictionary<object, object?> parents) {
        int depth = 0;
        for (object? current = node; current is not null; current = parents[current]) {
            if (Counts(current)) depth++;
        }
        return depth;
    }

    private static bool Counts(object node) =>
        node is (Statement or Expression or FnDecl) and not (BlockStmt or ExpressionStmt or GroupingExpr);

    // The errors of compiling a program. A host exception is reported as an error of its own,
    // naming the exception, so one crash does not hide the rest of the sample.
    private static IReadOnlyList<Diagnostic> Compile(string source) {
        var bag = new DiagnosticBag();
        try {
            CompilationUnit unit = Parser.Parse(Lexer.Scan(source, bag, "generated.grob"), bag);
            new TypeChecker(bag, Catalogue()).Check(unit);
            if (!bag.HasErrors) GrobCompiler.Compile(unit, bag);
        } catch (Exception ex) when (ex is not Xunit.Sdk.XunitException) {
            return [Diagnostic.Of(ErrorCatalog.E9001, SourceRange.Unknown, $"{ex.GetType().Name}: {ex.Message}")];
        }
        return [.. bag.Errors];
    }

    private static CompilationUnit Parse(string source) {
        var bag = new DiagnosticBag();
        return Parser.Parse(Lexer.Scan(source, bag, "generated.grob"), bag);
    }

    private static NativeCatalogue Catalogue() {
        HostFakes fakes = HostFakes.Create();
        return StdlibCatalogue.Build(fakes.RandomSource, fakes.Environment, fakes.Streams, fakes.Clock, fakes.FileSystem, verbose: false);
    }

    private static string Line(string source, int line) => source.Split('\n')[Math.Clamp(line, 1, int.MaxValue) - 1].Trim();

    private static string Numbered(string source) =>
        string.Join("\n", source.Split('\n').Select((line, i) => $"{i + 1,4}  {line}"));
}
