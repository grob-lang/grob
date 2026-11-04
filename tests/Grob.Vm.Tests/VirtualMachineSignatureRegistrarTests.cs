using Grob.Core;
using Grob.Runtime;

using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// The two <see cref="IPluginRegistrar"/> members D-426 change group 3 adds, as the VM
/// implements them until group 7 moves it onto a <see cref="NativeCatalogue"/>: the
/// signature-taking <see cref="VirtualMachine.RegisterNative(FunctionSignature, NativeFunction)"/>
/// writes the same global the untyped overload does, and
/// <see cref="VirtualMachine.RegisterNamedType"/> installs the definition's renderer.
/// </summary>
public sealed class VirtualMachineSignatureRegistrarTests {
    private static readonly FunctionSignature Greet =
        new("greeting.hello", [], SignatureType.Of(GrobType.String));

    private static Chunk CallGlobal(string name) {
        var chunk = new Chunk();
        int nameIdx = chunk.AddConstant(GrobValue.FromString(name));
        chunk.WriteOpCode(OpCode.GetGlobal, 1);
        chunk.WriteByte((byte)nameIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte(0, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    [Fact]
    public void RegisterNative_Typed_BindsTheGlobalUnderTheSignatureName() {
        var vm = new VirtualMachine(new StringWriter());
        var native = new NativeFunction("greeting.hello", 0, (_, _) => GrobValue.FromString("hello"));

        vm.RegisterNative(Greet, native);
        vm.Run(CallGlobal("greeting.hello"));

        Assert.Same(native, vm.Globals["greeting.hello"].AsFunction());
        Assert.Equal(GrobValue.FromString("hello"), vm.Stack.Peek());
    }

    [Fact]
    public void RegisterNative_Typed_NullSignature_Throws() {
        var vm = new VirtualMachine(new StringWriter());

        var ex = Assert.Throws<ArgumentNullException>(() =>
            vm.RegisterNative((FunctionSignature)null!, new NativeFunction("f", 0, (_, _) => GrobValue.Nil)));

        Assert.Equal("signature", ex.ParamName);
    }

    [Fact]
    public void RegisterNative_Typed_NullImplementation_Throws() {
        var vm = new VirtualMachine(new StringWriter());

        var ex = Assert.Throws<ArgumentNullException>(() => vm.RegisterNative(Greet, null!));

        Assert.Equal("implementation", ex.ParamName);
    }

    [Fact]
    public void RegisterNamedType_InstallsTheRenderer() {
        var vm = new VirtualMachine(new StringWriter());
        var definition = new NamedTypeDefinition("token", [], [], v => "<token>");
        var value = GrobValue.FromStruct(new GrobStruct("token", [new KeyValuePair<string, GrobValue>("secret", GrobValue.FromString("s"))]));

        vm.RegisterNamedType(definition);

        Assert.Equal("<token>", vm.RenderValue(value));
    }

    [Fact]
    public void RegisterNamedType_Null_Throws() {
        var vm = new VirtualMachine(new StringWriter());

        var ex = Assert.Throws<ArgumentNullException>(() => vm.RegisterNamedType(null!));

        Assert.Equal("definition", ex.ParamName);
    }
}
