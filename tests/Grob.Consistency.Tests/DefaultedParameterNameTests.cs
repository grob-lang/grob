using Grob.Core;
using Grob.DriftCheck;
using Xunit;

namespace Grob.Consistency.Tests;

/// <summary>
/// Check 4.1.7 (R-55, D-446 Decision 3) — a defaulted parameter's name is contract
/// once a native can be called with it by name. The check pins every defaulted
/// parameter of every core callable (and array <c>sort</c>'s <c>descending</c>) to the
/// signature the stdlib reference or the type registry shows, in both directions.
/// </summary>
public sealed class DefaultedParameterNameTests {
    private static DocumentedSignature Doc(string callable, params (string Name, string? Default)[] parameters) =>
        new("doc.md", 1, callable, [.. parameters.Select(p => new DocumentedParameter(p.Name, p.Default))]);

    private static readonly IReadOnlySet<string> NoCallables = new HashSet<string>();

    private static string TempFile(string content) {
        var path = Path.Join(Path.GetTempPath(), $"drift-sig-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, content);
        return path;
    }

    // --- The pure comparison ---

    [Fact]
    public void Check_EveryDefaultedParameterDocumented_Passes() {
        var code = new[] { new DefaultedParameter("padLeft", "char", "\" \"") };
        var docs = new[] { Doc("padLeft", ("width", null), ("char", "\" \"")) };

        var result = ConsistencyChecks.CheckDefaultedParameterNames(code, NoCallables, docs);

        Assert.True(result.Ok, string.Join("; ", result.Discrepancies));
    }

    [Fact]
    public void Check_DocumentNamesParameterDifferently_Fails() {
        var code = new[] { new DefaultedParameter("padLeft", "char", "\" \"") };
        var docs = new[] { Doc("padLeft", ("width", null), ("fill", "\" \"")) };

        var result = ConsistencyChecks.CheckDefaultedParameterNames(code, NoCallables, docs);

        Assert.False(result.Ok);
        var message = Assert.Single(result.Discrepancies);
        Assert.Contains("padLeft", message);
        Assert.Contains("char", message);
        Assert.Contains("fill", message);
    }

    [Fact]
    public void Check_NoDocumentShowsCallable_Fails() {
        var code = new[] { new DefaultedParameter("input", "prompt", "\"\"") };

        var result = ConsistencyChecks.CheckDefaultedParameterNames(code, NoCallables, []);

        Assert.False(result.Ok);
        var message = Assert.Single(result.Discrepancies);
        Assert.Contains("input", message);
        Assert.Contains("prompt", message);
        Assert.Contains("no signature", message);
    }

    [Fact]
    public void Check_DocumentShowsParameterAsRequired_Fails() {
        var code = new[] { new DefaultedParameter("truncate", "suffix", "\"...\"") };
        var docs = new[] { Doc("truncate", ("maxLength", null), ("suffix", null)) };

        var result = ConsistencyChecks.CheckDefaultedParameterNames(code, NoCallables, docs);

        Assert.False(result.Ok);
        Assert.Contains("required", Assert.Single(result.Discrepancies));
    }

    [Fact]
    public void Check_DefaultValuesDisagree_Fails() {
        var code = new[] { new DefaultedParameter("regex.compile", "timeout", "5") };
        var docs = new[] { Doc("regex.compile", ("pattern", null), ("timeout", "10")) };

        var result = ConsistencyChecks.CheckDefaultedParameterNames(code, NoCallables, docs);

        Assert.False(result.Ok);
        var message = Assert.Single(result.Discrepancies);
        Assert.Contains("5", message);
        Assert.Contains("10", message);
    }

    [Fact]
    public void Check_CodeRecordsNoDefaultValue_SkipsValueComparison() {
        // Array sort's descending has a name and a "has a default" flag, not a value.
        var code = new[] { new DefaultedParameter("sort", "descending", null) };
        var docs = new[] { Doc("sort", ("fn", null), ("descending", "false")) };

        var result = ConsistencyChecks.CheckDefaultedParameterNames(code, NoCallables, docs);

        Assert.True(result.Ok, string.Join("; ", result.Discrepancies));
    }

    [Fact]
    public void Check_OneSignatureAgrees_OthersDoNotOutvoteIt() {
        // A named-argument call example parses as a signature with no defaults; it
        // must not outvote the real signature.
        var code = new[] { new DefaultedParameter("regex.compile", "timeout", "5") };
        var docs = new[] {
            Doc("regex.compile", ("pattern", null), ("timeout", null)),
            Doc("regex.compile", ("pattern", null), ("flags", "\"\""), ("timeout", "5")),
        };

        var result = ConsistencyChecks.CheckDefaultedParameterNames(
            [code[0], new DefaultedParameter("regex.compile", "flags", "\"\"")], NoCallables, docs);

        Assert.True(result.Ok, string.Join("; ", result.Discrepancies));
    }

    [Fact]
    public void Check_DocumentDefaultsParameterCodeRequires_Fails() {
        // The strings.join case: documented separator: string = "", registered required.
        var registered = new HashSet<string> { "strings.join" };
        var docs = new[] { Doc("strings.join", ("parts", null), ("separator", "\"\"")) };

        var result = ConsistencyChecks.CheckDefaultedParameterNames([], registered, docs);

        Assert.False(result.Ok);
        var message = Assert.Single(result.Discrepancies);
        Assert.Contains("strings.join", message);
        Assert.Contains("separator", message);
    }

    [Fact]
    public void Check_DocumentedDefaultOnUnregisteredCallable_Passes() {
        // auth.apiKey belongs to a first-party plugin the core catalogue does not hold.
        var docs = new[] { Doc("auth.apiKey", ("key", null), ("headerName", "\"X-Api-Key\"")) };

        var result = ConsistencyChecks.CheckDefaultedParameterNames([], NoCallables, docs);

        Assert.True(result.Ok, string.Join("; ", result.Discrepancies));
    }

    [Fact]
    public void Check_SeveralDisagreements_ReportsEachOne() {
        var code = new[] {
            new DefaultedParameter("input", "prompt", "\"\""),
            new DefaultedParameter("date.parse", "pattern", "\"\""),
        };

        var result = ConsistencyChecks.CheckDefaultedParameterNames(code, NoCallables, []);

        Assert.Equal(2, result.Discrepancies.Count);
    }

    // --- The document parser ---

    [Fact]
    public void ParseDocumentedSignatures_BoldSignature_ReadsNamesAndDefaults() {
        var path = TempFile(
            "**`regex.compile(pattern: string, flags: string = \"\", timeout: int = 5): Regex`** builds.\n");
        try {
            var signature = Assert.Single(ConsistencyChecks.ParseDocumentedSignatures(path));

            Assert.Equal("regex.compile", signature.Callable);
            Assert.Equal(1, signature.Line);
            Assert.Equal(
                [("pattern", (string?)null), ("flags", "\"\""), ("timeout", "5")],
                signature.Parameters.Select(p => (p.Name, p.Default)).ToArray());
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseDocumentedSignatures_TableRowMethod_ReadsNameAndDefault() {
        var path = TempFile("| `padLeft(width: int, char: string = \" \")` | method | `→ string` | Pads. |\n");
        try {
            var signature = Assert.Single(ConsistencyChecks.ParseDocumentedSignatures(path));

            Assert.Equal("padLeft", signature.Callable);
            Assert.Equal("\" \"", signature.Parameters[1].Default);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseDocumentedSignatures_GenericMethodWithArrowType_ReadsBothParameters() {
        var path = TempFile("| `sort<U: Comparable>(fn: T → U, descending: bool = false)` | method | `→ T[]` | Sorts. |\n");
        try {
            var signature = Assert.Single(ConsistencyChecks.ParseDocumentedSignatures(path));

            Assert.Equal("sort", signature.Callable);
            Assert.Equal(["fn", "descending"], signature.Parameters.Select(p => p.Name).ToArray());
            Assert.Equal("false", signature.Parameters[1].Default);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseDocumentedSignatures_LambdaArrowInAType_IsNotAClosingDelimiter() {
        // The '>' of '=>' must not close a bracket: depth would go negative and the next
        // top-level comma would be ignored, losing 'limit' and its default.
        var path = TempFile("`each(callback: (int) => int, limit: int = 5)`\n");
        try {
            var signature = Assert.Single(ConsistencyChecks.ParseDocumentedSignatures(path));

            Assert.Equal(["callback", "limit"], signature.Parameters.Select(p => p.Name).ToArray());
            Assert.Null(signature.Parameters[0].Default);
            Assert.Equal("5", signature.Parameters[1].Default);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseDocumentedSignatures_CommaInsideParenthesisedType_KeepsOneParameter() {
        var path = TempFile("`each(callback: fn(a: int, b: int): bool, limit: int = 3)`\n");
        try {
            var signature = Assert.Single(ConsistencyChecks.ParseDocumentedSignatures(path));

            Assert.Equal(["callback", "limit"], signature.Parameters.Select(p => p.Name).ToArray());
            Assert.Equal("3", signature.Parameters[1].Default);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseDocumentedSignatures_PositionalCallExamples_AreIgnored() {
        var path = TempFile(
            "d := date.parse(\"05/04/2026\", \"dd/MM/yyyy\")\n" +
            "items.sort(i => i.size, descending: true)\n" +
            "`date.parse(value: string, pattern: string = \"\"): date`\n");
        try {
            var signature = Assert.Single(ConsistencyChecks.ParseDocumentedSignatures(path));

            Assert.Equal("date.parse", signature.Callable);
            Assert.Equal(3, signature.Line);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseDocumentedSignatures_NoSignatureInAnyDocument_ThrowsAnchorNotFound() {
        var path = TempFile("# Prose only\n\nNothing with a signature here.\n");
        try {
            var ex = Assert.Throws<AnchorNotFoundException>(
                () => ConsistencyChecks.ParseDocumentedSignatures(path));
            Assert.Contains("signature", ex.Anchor);
        } finally {
            File.Delete(path);
        }
    }

    // --- The live registry against the live documents ---

    [Fact]
    public void ActualDefaultedParameters_LiveCatalogue_ReadsEveryDefault() {
        var actual = ConsistencyChecks.ActualDefaultedParameters()
            .Select(p => $"{p.Callable}.{p.Name}")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // Seven registered defaults on six callables (gate report 1.3) plus sort's.
        Assert.Equal(
            [
                "date.parse.pattern", "input.prompt", "padLeft.char", "padRight.char",
                "regex.compile.flags", "regex.compile.timeout", "sort.descending", "truncate.suffix",
            ],
            actual);
    }

    [Fact]
    public void ActualDefaultedParameters_SignatureHoldsValue_RecordsItRendered() {
        var byKey = ConsistencyChecks.ActualDefaultedParameters()
            .ToDictionary(p => $"{p.Callable}.{p.Name}", p => p.Default);

        Assert.Equal("\"\"", byKey["date.parse.pattern"]);
        Assert.Equal("5", byKey["regex.compile.timeout"]);
        Assert.Equal("\" \"", byKey["padLeft.char"]);
        Assert.Null(byKey["sort.descending"]);
    }

    [Fact]
    public void RenderDefault_EveryScalarKind_WritesItAsTheDocumentsDo() {
        Assert.Equal("\"x\"", ConsistencyChecks.RenderDefault(GrobValue.FromString("x")));
        Assert.Equal("-3", ConsistencyChecks.RenderDefault(GrobValue.FromInt(-3)));
        Assert.Equal("1.5", ConsistencyChecks.RenderDefault(GrobValue.FromFloat(1.5)));
        Assert.Equal("true", ConsistencyChecks.RenderDefault(GrobValue.FromBool(true)));
        Assert.Equal("false", ConsistencyChecks.RenderDefault(GrobValue.FromBool(false)));
        Assert.Equal("nil", ConsistencyChecks.RenderDefault(default));
    }

    [Fact]
    public void ActualRegisteredCallables_LiveCatalogue_NamesEveryKind() {
        var callables = ConsistencyChecks.ActualRegisteredCallables();

        Assert.Contains("strings.join", callables);
        Assert.Contains("date.parse", callables);
        Assert.Contains("padLeft", callables);
        Assert.Contains("input", callables);
        Assert.Contains("sort", callables);
    }

    [Fact]
    public void DefaultedParameterNames_LiveTree_MatchTheDocuments() {
        var result = ConsistencyChecks.CheckDefaultedParameterNames(
            ConsistencyChecks.ActualDefaultedParameters(),
            ConsistencyChecks.ActualRegisteredCallables(),
            ConsistencyChecks.ParseDocumentedSignatures(RepoPaths.StdlibReference, RepoPaths.TypeRegistry));

        Assert.True(result.Ok, string.Join(Environment.NewLine, result.Discrepancies));
    }

    [Fact]
    public void RunAll_Always_IncludesTheDefaultedParameterNameCheck() {
        var names = ConsistencyChecks.RunAll().Select(r => r.Name).ToList();

        Assert.Contains("defaulted parameter names", names);
    }
}
