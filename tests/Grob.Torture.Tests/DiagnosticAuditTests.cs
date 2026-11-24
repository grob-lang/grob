using Xunit;

using Grob.Core;

namespace Grob.Torture.Tests;

/// <summary>
/// A diagnostic must carry a registered code and a location inside the source (gate report §1.1,
/// corrections). The start is checked strictly; the end only against end of file, because the
/// parser ends many nodes at the next token's start (F-7).
/// </summary>
public sealed class DiagnosticAuditTests {
    private const string File = "program.grob";
    private const string Source = "x := 1\nyy := 22\n";

    private static IReadOnlyList<Finding> Audit(Diagnostic diagnostic, PipelinePhase phase, string source = Source) =>
        DiagnosticAudit.Audit(diagnostic, phase, DiagnosticAudit.LineLengths(source), File);

    private static Diagnostic At(string code, SourceLocation start, SourceLocation end) =>
        new(code, "message", new SourceRange(start, end), Severity.Error);

    private static Diagnostic At(string code, int line, int column) =>
        At(code, new SourceLocation(File, line, column), new SourceLocation(File, line, column));

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 9)]
    [InlineData(3, 1)]
    public void Audit_StartInsideTheSource_FindsNothing(int line, int column) {
        Assert.Empty(Audit(At(ErrorCatalog.E0002.Code, line, column), PipelinePhase.Check));
    }

    [Theory]
    [InlineData(4, 1, "line")]
    [InlineData(1, 8, "column")]
    [InlineData(2, 10, "column")]
    [InlineData(3, 2, "column")]
    public void Audit_StartOutsideTheSource_IsLocationFinding(int line, int column, string check) {
        Finding finding = Assert.Single(
            Audit(At(ErrorCatalog.E0002.Code, line, column), PipelinePhase.Check));

        Assert.Equal(new Signature(PipelinePhase.Check, FindingClass.Location, $"{ErrorCatalog.E0002.Code}|{check}"), finding.Signature);
    }

    // SourceLocation's constructor refuses a zero line or column, but its init accessors do not.
    [Theory]
    [InlineData(0, 1, "line")]
    [InlineData(1, 0, "column")]
    public void Audit_ZeroLineOrColumn_IsLocationFinding(int line, int column, string check) {
        SourceLocation start = new SourceLocation(File, 1, 1) with { Line = line, Column = column };

        Finding finding = Assert.Single(Audit(At(ErrorCatalog.E0002.Code, start, start), PipelinePhase.Check));

        Assert.Equal(new Signature(PipelinePhase.Check, FindingClass.Location, $"{ErrorCatalog.E0002.Code}|{check}"), finding.Signature);
    }

    [Fact]
    public void Audit_UnknownLocation_IsFileFinding() {
        Diagnostic diagnostic = new(ErrorCatalog.E0002.Code, "message", SourceRange.Unknown, Severity.Error);

        Finding finding = Assert.Single(Audit(diagnostic, PipelinePhase.Parse));

        Assert.Equal(new Signature(PipelinePhase.Parse, FindingClass.Location, $"{ErrorCatalog.E0002.Code}|file"), finding.Signature);
    }

    [Fact]
    public void Audit_EndPastEndOfFile_IsEndFinding() {
        Diagnostic diagnostic = At(ErrorCatalog.E0002.Code, new SourceLocation(File, 2, 1), new SourceLocation(File, 3, 5));

        Finding finding = Assert.Single(Audit(diagnostic, PipelinePhase.Check));

        Assert.Equal(new Signature(PipelinePhase.Check, FindingClass.Location, $"{ErrorCatalog.E0002.Code}|end"), finding.Signature);
    }

    [Fact]
    public void Audit_EndOnTheNextTokensStart_FindsNothing() {
        Diagnostic diagnostic = At(ErrorCatalog.E0002.Code, new SourceLocation(File, 1, 1), new SourceLocation(File, 2, 1));

        Assert.Empty(Audit(diagnostic, PipelinePhase.Check));
    }

    [Fact]
    public void Audit_UnregisteredCode_IsUnregisteredFinding() {
        Finding finding = Assert.Single(Audit(At("X0001", 1, 1), PipelinePhase.Compile));

        Assert.Equal(new Signature(PipelinePhase.Compile, FindingClass.Unregistered, "X0001"), finding.Signature);
    }

    // The lexer advances its column past a '\r', so a CRLF line's newline token sits one column
    // after the '\r', and the line is measured with it.
    [Fact]
    public void Audit_CarriageReturnLineEndings_CountTheReturnAsTheLexerDoes() {
        const string crlf = "x := 1\r\nyy := 22\r\n";

        Assert.Empty(Audit(At(ErrorCatalog.E0002.Code, 1, 8), PipelinePhase.Check, crlf));
        Assert.Single(Audit(At(ErrorCatalog.E0002.Code, 1, 9), PipelinePhase.Check, crlf));
    }

    [Fact]
    public void Audit_ALoneCarriageReturnAtEndOfFile_IsCountedToo() {
        const string source = "x := 1\r";

        Assert.Empty(Audit(At(ErrorCatalog.E0002.Code, 1, 8), PipelinePhase.Check, source));
        Assert.Single(Audit(At(ErrorCatalog.E0002.Code, 1, 9), PipelinePhase.Check, source));
    }

    // Through the whole pipeline: a diagnostic the parser reports at a CRLF newline is inside
    // the source, so the run has diagnostics and no finding.
    [Fact]
    public void Oracle_DiagnosticAtACarriageReturnNewline_IsNotALocationFinding() {
        OracleResult result = new Oracle(OracleBudget.Ci).Run("fn\r\n");

        Assert.Equal(OutcomeKind.Diagnostics, result.Outcome);
        Assert.Empty(result.Findings);
    }
}
