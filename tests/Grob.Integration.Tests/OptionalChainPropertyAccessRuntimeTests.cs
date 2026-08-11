using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// End-to-end tests for the D-403 fix: <c>?.</c> property access on a nullable
/// primitive receiver (<c>s?.length</c>) must still short-circuit to <c>nil</c> at
/// runtime exactly as before, proving <see cref="Grob.Compiler.TypeChecker"/>'s new
/// nullable-widened property typing does not silently reroute the compiler around
/// D-400's <c>IsNil</c>-guarded emission path.
/// </summary>
/// <remarks>
/// Deliberately does not exercise a non-nil primitive receiver's <c>?.</c> property
/// access (e.g. <c>s?.length</c> where <c>s</c> is a non-nil <c>string?</c>) — that
/// path was already broken before this branch and remains so, a separate,
/// pre-existing <c>OpCode.GetProperty</c> VM-dispatch gap
/// (<c>VirtualMachine.cs</c> has no <c>String</c>/<c>Int</c>/<c>Float</c>/<c>Bool</c>
/// arm) that <c>OptionalChainMethodCallTests.
/// ChainedCallOnNonNilReceiver_EvaluatesEachLink</c>'s own comment already documents
/// for the method-call case (<c>s?.upper()</c>) and this entry does not fix.
/// </remarks>
public sealed class OptionalChainPropertyAccessRuntimeTests {
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

    private static string RunAndAssertSuccess(string source) {
        (string stdout, string stderr, int exitCode) = RunSource(source);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal(0, exitCode);
        return stdout;
    }

    [Fact]
    public void NilStringReceiver_PropertyAccess_ShortCircuitsToNil() {
        // Load-bearing: without ResolveNullableMemberAccess clearing
        // MemberAccessExpr.ResolvedPrimitiveNativeName, the compiler's own
        // VisitMemberAccess checks that field BEFORE IsOptional and would emit the
        // unguarded qualified-native rewrite instead of the IsNil-guarded generic
        // GetProperty path — passing a nil receiver straight into 'string.length'
        // with no check at all, a worse failure than the pre-existing permissive gap.
        string stdout = RunAndAssertSuccess(
            "s: string? := nil\n" +
            "print(s?.length)\n");

        Assert.Equal($"nil{NL}", stdout);
    }

    [Fact]
    public void NilStructReceiver_FieldAccess_ShortCircuitsToNil() {
        string stdout = RunAndAssertSuccess(
            "type Point {\n" +
            "    x: int\n" +
            "    y: int\n" +
            "}\n" +
            "p: Point? := nil\n" +
            "print(p?.x)\n");

        Assert.Equal($"nil{NL}", stdout);
    }
}
