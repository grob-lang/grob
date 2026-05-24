using Grob.Core;
using Xunit;

namespace Grob.Core.Tests;

public sealed class DiagnosticBagTests {
    private static readonly SourceRange _range = new(new SourceLocation("test.grob", 1, 1));

    private static Diagnostic MakeDiagnostic(Severity severity, string code = "E0001") =>
        new(code, "message", _range, severity);

    // Empty bag

    [Fact]
    public void NewBag_IsEmpty() {
        var bag = new DiagnosticBag();

        Assert.Equal(0, bag.Count);
        Assert.False(bag.HasErrors);
        Assert.False(bag.HasWarnings);
        Assert.Empty(bag.Diagnostics);
        Assert.Empty(bag.Errors);
        Assert.Empty(bag.Warnings);
        Assert.Empty(bag.Infos);
        Assert.Empty(bag.Hints);
    }

    // Null guard

    [Fact]
    public void Add_Null_ThrowsArgumentNullException() {
        var bag = new DiagnosticBag();

        Assert.Throws<ArgumentNullException>(() => bag.Add(null!));
    }

    // Single severity additions

    [Fact]
    public void Add_ErrorDiagnostic_HasErrorsTrue() {
        var bag = new DiagnosticBag();
        var diagnostic = MakeDiagnostic(Severity.Error);

        bag.Add(diagnostic);

        Assert.Equal(1, bag.Count);
        Assert.True(bag.HasErrors);
        Assert.Contains(diagnostic, bag.Errors);
        Assert.Empty(bag.Warnings);
        Assert.Empty(bag.Infos);
        Assert.Empty(bag.Hints);
    }

    [Fact]
    public void Add_WarningDiagnostic_HasWarningsTrueHasErrorsFalse() {
        var bag = new DiagnosticBag();
        var diagnostic = MakeDiagnostic(Severity.Warning);

        bag.Add(diagnostic);

        Assert.True(bag.HasWarnings);
        Assert.False(bag.HasErrors);
    }

    // Mixed severities

    [Fact]
    public void Add_AllFourSeverities_EachFilteredEnumerableContainsOne() {
        var bag = new DiagnosticBag();
        var error = MakeDiagnostic(Severity.Error, "E0001");
        var warning = MakeDiagnostic(Severity.Warning, "W0001");
        var info = MakeDiagnostic(Severity.Info, "I0001");
        var hint = MakeDiagnostic(Severity.Hint, "H0001");

        bag.Add(error);
        bag.Add(warning);
        bag.Add(info);
        bag.Add(hint);

        Assert.Single(bag.Errors);
        Assert.Single(bag.Warnings);
        Assert.Single(bag.Infos);
        Assert.Single(bag.Hints);
        Assert.True(bag.HasErrors);
        Assert.True(bag.HasWarnings);
    }

    // Insertion order — Diagnostics property

    [Fact]
    public void Diagnostics_PreservesInsertionOrder() {
        var bag = new DiagnosticBag();
        var first = MakeDiagnostic(Severity.Error, "E0001");
        var second = MakeDiagnostic(Severity.Warning, "W0001");
        var third = MakeDiagnostic(Severity.Info, "I0001");

        bag.Add(first);
        bag.Add(second);
        bag.Add(third);

        Assert.Equal([first, second, third], bag.Diagnostics);
    }

    // Insertion order — direct enumeration

    [Fact]
    public void Enumeration_PreservesInsertionOrder() {
        var bag = new DiagnosticBag();
        var first = MakeDiagnostic(Severity.Error, "E0001");
        var second = MakeDiagnostic(Severity.Warning, "W0001");

        bag.Add(first);
        bag.Add(second);

        Assert.Equal(new[] { first, second }, bag.ToList());
    }

    // Insertion order — filtered enumerables

    [Fact]
    public void FilteredEnumerables_PreserveOriginalInsertionOrder() {
        var bag = new DiagnosticBag();
        var first = MakeDiagnostic(Severity.Error, "E0001");
        var second = MakeDiagnostic(Severity.Warning, "W0001");
        var third = MakeDiagnostic(Severity.Error, "E0002");

        bag.Add(first);
        bag.Add(second);
        bag.Add(third);

        Assert.Equal(new[] { first, third }, bag.Errors.ToList());
    }

    // No cap

    [Fact]
    public void Add_ManyDiagnostics_AllAccepted() {
        var bag = new DiagnosticBag();

        for (var i = 0; i < 1000; i++)
            bag.Add(MakeDiagnostic(Severity.Error));

        Assert.Equal(1000, bag.Count);
    }

    [Fact]
    public void DiagnosticBag_NonGenericEnumerator_Iterates() {
        var bag = new DiagnosticBag {
            MakeDiagnostic(Severity.Error)
        };

        System.Collections.IEnumerable nonGeneric = bag;
        var enumerator = nonGeneric.GetEnumerator();

        Assert.True(enumerator.MoveNext());
    }
}
