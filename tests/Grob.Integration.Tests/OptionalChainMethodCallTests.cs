using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// End-to-end tests for the D-380/D-400 fix: <c>?.</c> on a method call
/// (<c>xs?.first()</c>) must short-circuit to <c>nil</c> on a nil receiver exactly as
/// the property path (<c>xs?.length</c>) already does, instead of crashing the VM with
/// an internal "Call target is not a function" error. Driven through the CLI's own
/// <see cref="RunCommand"/> over a real <c>.grob</c> file — the same harness shape as
/// <c>Sprint9IncrementA1aTests.RunSource</c> — so each case asserts the full CLI
/// contract (stdout, stderr and exit code) rather than only a VM writer's output, and
/// exercises plugin auto-registration on the real command path.
/// </summary>
public sealed class OptionalChainMethodCallTests {
    private static string NL => Environment.NewLine;

    private static (string Stdout, string Stderr, int ExitCode) RunSource(string source) {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.grob");
        File.WriteAllText(path, source);
        try {
            var stdout = new StringWriter(new StringBuilder());
            var stderr = new StringWriter(new StringBuilder());
            int exitCode = new RunCommand(stdout, stderr).Run(path);
            return (stdout.ToString(), stderr.ToString(), exitCode);
        } finally {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Runs <paramref name="source"/> and asserts the success half of the CLI
    /// contract — clean stderr and exit code 0 — before returning stdout for the
    /// caller's own value assertion. A short-circuit that crashed the VM would fail
    /// here on the exit code, not only on the printed value.
    /// </summary>
    private static string RunAndAssertSuccess(string source) {
        (string stdout, string stderr, int exitCode) = RunSource(source);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal(0, exitCode);
        return stdout;
    }

    // -----------------------------------------------------------------------
    // The reported case, and the receiver-kind matrix (array/string/numeric/nominal/
    // map). Map was excluded from this matrix originally — GrobType had no
    // NullableMap variant, so a nil map receiver could not be constructed from source
    // at all — closed by D-401, which added GrobType.NullableMap. The short-circuit
    // mechanism itself (EmitOptionalChainCall) was always receiver-type-agnostic, so
    // no compiler/VM change was needed here — only the type existing to compile
    // 'm: map<string, int>? := nil' in the first place.
    // -----------------------------------------------------------------------

    [Fact]
    public void NilArrayReceiver_ShortCircuitsToNil() {
        string stdout = RunAndAssertSuccess(
            "xs: int[]? := nil\n" +
            "print(xs?.first())\n");

        Assert.Equal($"nil{NL}", stdout);
    }

    [Fact]
    public void NilStringReceiver_ShortCircuitsToNil() {
        string stdout = RunAndAssertSuccess(
            "s: string? := nil\n" +
            "print(s?.upper())\n");

        Assert.Equal($"nil{NL}", stdout);
    }

    [Fact]
    public void NilNumericReceiver_ShortCircuitsToNil() {
        string stdout = RunAndAssertSuccess(
            "n: int? := nil\n" +
            "print(n?.toString())\n");

        Assert.Equal($"nil{NL}", stdout);
    }

    [Fact]
    public void NilNominalReceiver_ShortCircuitsToNil() {
        string stdout = RunAndAssertSuccess(
            "d: date? := nil\n" +
            "print(d?.addDays(1))\n");

        Assert.Equal($"nil{NL}", stdout);
    }

    [Fact]
    public void NilMapReceiver_ShortCircuitsToNil() {
        // D-401: map<K,V>? did not exist until this fix, so this case could not be
        // constructed from source before — closing the gap the matrix comment above
        // used to note explicitly.
        string stdout = RunAndAssertSuccess(
            "m: map<string, int>? := nil\n" +
            "print(m?.get(\"k\"))\n");

        Assert.Equal($"nil{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Regression guards
    // -----------------------------------------------------------------------

    [Fact]
    public void NilArrayPropertyAccess_StillShortCircuits() {
        // The pre-existing, already-correct property path this fix must not regress.
        string stdout = RunAndAssertSuccess(
            "xs: int[]? := nil\n" +
            "print(xs?.length)\n");

        Assert.Equal($"nil{NL}", stdout);
    }

    [Fact]
    public void NonNilReceiver_DispatchesNormally() {
        string stdout = RunAndAssertSuccess(
            "xs: int[]? := [10, 20, 30]\n" +
            "print(xs?.first())\n");

        Assert.Equal($"10{NL}", stdout);
    }

    [Fact]
    public void NonNilMapReceiver_DispatchesNormally() {
        string stdout = RunAndAssertSuccess(
            "m: map<string, int>? := map<string, int>{\"k\": 42}\n" +
            "print(m?.get(\"k\"))\n");

        Assert.Equal($"42{NL}", stdout);
    }

    [Fact]
    public void NilMapPropertyAccess_StillShortCircuits() {
        string stdout = RunAndAssertSuccess(
            "m: map<string, int>? := nil\n" +
            "print(m?.length)\n");

        Assert.Equal($"nil{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Argument evaluation — load-bearing: must not evaluate on the nil path.
    // -----------------------------------------------------------------------

    [Fact]
    public void NilReceiverArguments_AreNotEvaluated() {
        string stdout = RunAndAssertSuccess(
            "fn sideEffect(): int {\n" +
            "    print(\"SIDE EFFECT RAN\")\n" +
            "    return 1\n" +
            "}\n" +
            "xs: int[]? := nil\n" +
            "print(xs?.contains(sideEffect()))\n");

        Assert.Equal($"nil{NL}", stdout);
        Assert.DoesNotContain("SIDE EFFECT RAN", stdout);
    }

    [Fact]
    public void NonNilReceiverArguments_AreEvaluated() {
        // Contrast case: proves the guard above suppresses evaluation because the
        // receiver is nil, not because arguments are never evaluated at all.
        string stdout = RunAndAssertSuccess(
            "fn sideEffect(): int {\n" +
            "    print(\"SIDE EFFECT RAN\")\n" +
            "    return 1\n" +
            "}\n" +
            "xs: int[]? := [1, 2, 3]\n" +
            "print(xs?.contains(sideEffect()))\n");

        Assert.Contains("SIDE EFFECT RAN", stdout);
    }

    // -----------------------------------------------------------------------
    // Chained short-circuit — the whole chain skips from the first nil, once.
    // -----------------------------------------------------------------------

    [Fact]
    public void ChainedCallOnNilReceiver_ShortCircuitsWholeChain() {
        string stdout = RunAndAssertSuccess(
            "a: string[]? := nil\n" +
            "print(a?.first()?.upper())\n");

        Assert.Equal($"nil{NL}", stdout);
    }

    [Fact]
    public void ChainedCallOnNonNilReceiver_EvaluatesEachLink() {
        // Array-of-array (not string) deliberately: GetProperty dispatches array/map/
        // struct receivers by their actual runtime kind regardless of static type, but
        // has no arm for a raw String receiver — a real, separate, pre-existing gap
        // (confirmed: `s?.upper()` on a *non-nil* `string?` already crashes today with
        // "opcode GetProperty 'upper' on receiver of kind String not yet implemented",
        // independent of this fix). Reported, not fixed here — out of this brief's scope.
        string stdout = RunAndAssertSuccess(
            "a: int[][]? := [[1, 2, 3]]\n" +
            "print(a?.first()?.first())\n");

        Assert.Equal($"1{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Interaction with '??'
    // -----------------------------------------------------------------------

    [Fact]
    public void NilReceiverWithNilCoalesce_UnwrapsToFallback() {
        string stdout = RunAndAssertSuccess(
            "xs: int[]? := nil\n" +
            "print(xs?.first() ?? 0)\n");

        Assert.Equal($"0{NL}", stdout);
    }
}
