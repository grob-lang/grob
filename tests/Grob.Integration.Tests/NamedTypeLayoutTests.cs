using System.Reflection;

using Grob.Compiler;
using Grob.Core;
using Grob.Stdlib;
using Grob.Vm;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// R-19's closing criterion: the <c>date</c> and <c>guid</c> layouts are defined once, in
/// <c>Grob.Core</c>'s <see cref="DateLayout"/> and <see cref="GuidLayout"/>, and the
/// assemblies that build or read those values hold no second copy. A constant re-declared
/// with the same value elsewhere is the drift R-19 recorded: it compiles, and nothing
/// fails until the two copies disagree. <see cref="DateLayout.RoundTripFormat"/> is not
/// scanned: the VM's and the plugin's ISO rendering formats legitimately share its
/// value, and it has been declared only in <c>Grob.Core</c> since D-357/D-367.
/// </summary>
public sealed class NamedTypeLayoutTests {
    private static readonly Assembly[] Readers = [
        typeof(TypeChecker).Assembly,
        typeof(VirtualMachine).Assembly,
        typeof(StdlibPlugins).Assembly,
    ];

    public static TheoryData<string> LayoutValues => [
        DateLayout.ValueFieldName,
        DateLayout.TypeName,
        GuidLayout.TypeName,
    ];

    [Theory]
    [MemberData(nameof(LayoutValues))]
    public void NoReaderAssembly_RedeclaresALayoutConstant(string value) {
        string[] copies = [.. Readers
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string) && (string?)field.GetRawConstantValue() == value)
                .Select(field => $"{type.FullName}.{field.Name}"))];

        Assert.Empty(copies);
    }
}
