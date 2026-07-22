using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Unit tests for <see cref="NativeDefaultArgumentFill"/> (D-358) — the pure
/// <c>(supplied, arity, defaults)</c> helper the compiler's namespace-native emission
/// branch calls to synthesise the trailing constant arguments a call site omitted.
/// </summary>
public sealed class NativeDefaultArgumentFillTests {
    [Fact]
    public void Resolve_FullySupplied_ReturnsEmptyList() {
        IReadOnlyList<GrobValue?> defaults = [null, GrobValue.FromString("")];

        IReadOnlyList<GrobValue> fill = NativeDefaultArgumentFill.Resolve(2, 2, defaults);

        Assert.Empty(fill);
    }

    [Fact]
    public void Resolve_PartiallySupplied_ReturnsTrailingDefaultsInOrder() {
        IReadOnlyList<GrobValue?> defaults = [null, GrobValue.FromString("b"), GrobValue.FromString("c")];

        IReadOnlyList<GrobValue> fill = NativeDefaultArgumentFill.Resolve(1, 3, defaults);

        Assert.Equal([GrobValue.FromString("b"), GrobValue.FromString("c")], fill);
    }

    [Fact]
    public void Resolve_NoDefaultsDeclared_ReturnsEmptyList() {
        IReadOnlyList<GrobValue> fill = NativeDefaultArgumentFill.Resolve(2, 2, defaults: null);

        Assert.Empty(fill);
    }

    [Fact]
    public void Resolve_MissingDeclaredDefault_Throws() {
        // A slot within the requested fill range with no default is a programmer error —
        // the type checker is what guarantees every filled slot has one before emission
        // ever reaches here (CheckNativeCall's required/full arity range).
        IReadOnlyList<GrobValue?> defaults = [null, null];

        Assert.Throws<InvalidOperationException>(() => NativeDefaultArgumentFill.Resolve(1, 2, defaults));
    }
}
