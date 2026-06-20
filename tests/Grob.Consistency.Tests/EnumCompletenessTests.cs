using Grob.DriftCheck;
using Xunit;

namespace Grob.Consistency.Tests;

/// <summary>
/// Check 4.1.4 — OpCode and TokenKind completeness. Every opcode the spec lists
/// complete from Sprint 2 (§3.3) and every token the spec lists complete from
/// Sprint 1 (§3.4) must exist in the corresponding enum. The "spec says X exists
/// — does the code have X" check.
/// </summary>
public sealed class EnumCompletenessTests {
    // --- Negative proofs ---

    [Fact]
    public void EnumCompleteness_FailsWhenASpecNameIsMissingFromTheEnum() {
        var declared = new[] { "Constant", "AddInt", "GhostOp" };
        var actual = new HashSet<string> { "Constant", "AddInt" };

        var result = ConsistencyChecks.CheckEnumCompleteness("OpCode", declared, actual);

        Assert.False(result.Ok);
        Assert.Contains(result.Discrepancies, d => d.Contains("GhostOp") && d.Contains("absent"));
    }

    [Fact]
    public void TokenKind_FailsWhenSpecListsAnUnmappedAtom() {
        var atoms = new[] { new SpecAtom("Operators", ">>>") };
        var actual = ConsistencyChecks.ActualTokenKindNames();

        var result = ConsistencyChecks.CheckTokenKindCompleteness(atoms, actual);

        Assert.False(result.Ok);
        Assert.Contains(result.Discrepancies, d => d.Contains(">>>") && d.Contains("no mapping"));
    }

    [Fact]
    public void TokenKind_FailsWhenMappedMemberIsAbsentFromTheEnum() {
        var atoms = new[] { new SpecAtom("Keywords", "fn") };
        var actual = new HashSet<string>(); // empty enum — Fn is absent

        var result = ConsistencyChecks.CheckTokenKindCompleteness(atoms, actual);

        Assert.False(result.Ok);
        Assert.Contains(result.Discrepancies, d => d.Contains("TokenKind.Fn") && d.Contains("absent"));
    }

    // --- Positive proofs: spec §3.3 / §3.4 against the live enums ---

    [Fact]
    public void EverySpecOpCode_ExistsInTheEnum() {
        var declared = ConsistencyChecks.ParseSpecOpCodes(RepoPaths.Requirements);
        var actual = ConsistencyChecks.ActualOpCodeNames();

        var result = ConsistencyChecks.CheckEnumCompleteness("OpCode", declared, actual);

        Assert.True(result.Ok, string.Join("; ", result.Discrepancies));
        Assert.True(declared.Count >= 60, $"Parsed only {declared.Count} opcodes from §3.3 — parser may be broken.");
    }

    [Fact]
    public void EverySpecTokenKind_ExistsInTheEnum() {
        var atoms = ConsistencyChecks.ParseSpecTokenAtoms(RepoPaths.Requirements);
        var actual = ConsistencyChecks.ActualTokenKindNames();

        var result = ConsistencyChecks.CheckTokenKindCompleteness(atoms, actual);

        Assert.True(result.Ok, string.Join("; ", result.Discrepancies));
        Assert.True(atoms.Count >= 60, $"Parsed only {atoms.Count} token atoms from §3.4 — parser may be broken.");
    }
}
