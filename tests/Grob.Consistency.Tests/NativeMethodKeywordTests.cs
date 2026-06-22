using Grob.DriftCheck;
using Xunit;

namespace Grob.Consistency.Tests;

/// <summary>
/// Check 4.1.6 (D-320) — no registered native method name may be a hard keyword.
/// A hard keyword lexes to a keyword token, so the member-access parser (which
/// expects an identifier after <c>.</c>) cannot parse a call to a method of that
/// name. This is the durable guard that turns the next such collision into a build
/// failure instead of an unparseable call. Reserved identifiers (<c>formatAs</c>,
/// <c>select</c>) are deliberately permitted as method names — the check is against
/// the hard-keyword set, not the reserved-identifier set.
/// </summary>
public sealed class NativeMethodKeywordTests {
    private static IReadOnlySet<string> HardKeywordLexemes() =>
        ConsistencyChecks.ParseSpecTokenAtoms(RepoPaths.Requirements)
            .Where(a => a.Section == "Keywords")
            .Select(a => a.Text)
            .ToHashSet(StringComparer.Ordinal);

    // --- Negative proof: a planted native method named like a keyword fails ---

    [Fact]
    public void Collision_FailsWhenANativeMethodIsAHardKeyword() {
        var natives = new HashSet<string>(StringComparer.Ordinal) { "filter", "case" };
        var keywords = new HashSet<string>(StringComparer.Ordinal) { "case", "default", "switch" };

        var result = ConsistencyChecks.CheckNativeMethodsAvoidKeywords(natives, keywords);

        Assert.False(result.Ok);
        Assert.Contains(result.Discrepancies, d => d.Contains("case"));
    }

    // --- Positive proof: reserved identifiers as method names are allowed ---

    [Fact]
    public void Collision_PassesWhenAMethodNameIsAReservedIdentifier() {
        // 'select' is a registered native method and a reserved identifier, but it
        // is NOT a hard keyword (D-320), so this must pass.
        var natives = new HashSet<string>(StringComparer.Ordinal) { "filter", "select", "sort", "each" };
        var keywords = new HashSet<string>(StringComparer.Ordinal) { "case", "default", "switch" };

        var result = ConsistencyChecks.CheckNativeMethodsAvoidKeywords(natives, keywords);

        Assert.True(result.Ok, string.Join("; ", result.Discrepancies));
    }

    // --- Positive proof: the live registry against the live keyword set ---

    [Fact]
    public void EveryRegisteredNativeMethod_AvoidsTheHardKeywords() {
        var natives = ConsistencyChecks.ParseArrayNativeMethodNames(RepoPaths.ArrayNatives);
        var keywords = HardKeywordLexemes();

        var result = ConsistencyChecks.CheckNativeMethodsAvoidKeywords(natives, keywords);

        Assert.True(result.Ok, string.Join("; ", result.Discrepancies));
        Assert.Contains("select", natives); // parser sanity: the registry was actually read
        Assert.True(keywords.Count >= 20, $"Parsed only {keywords.Count} keyword lexemes from §3.4 — parser may be broken.");
    }
}
