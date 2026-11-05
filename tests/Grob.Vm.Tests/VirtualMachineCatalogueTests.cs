using Grob.Core;
using Grob.Runtime;

using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// The VM constructed over a <see cref="NativeCatalogue"/> (D-426 Decision 2, change
/// group 7): the catalogue's globals are the VM's globals, its renderers drive
/// <see cref="OpCode.Print"/>, and <see cref="OpCode.GetProperty"/> on a registered named
/// type dispatches through the catalogue's bindings. The VM is no longer a registrar;
/// there is one registration path and this is where it lands. Chunks are hand-built, as
/// everywhere in this project.
/// </summary>
public sealed class VirtualMachineCatalogueTests {
    private sealed class Plugin(string name, Action<IPluginRegistrar> register) : IGrobPlugin {
        public string Name => name;

        public void Register(IPluginRegistrar registrar) => register(registrar);
    }

    private static readonly SignatureType.Concrete Int = SignatureType.Of(GrobType.Int);
    private static readonly SignatureType.Concrete String = SignatureType.Of(GrobType.String);

    private static NativeCatalogue Catalogue(params IGrobPlugin[] plugins) {
        var builder = new NativeCatalogueBuilder();
        foreach (IGrobPlugin plugin in plugins) builder.Add(plugin);
        return builder.Build();
    }

    private static GrobValue Token(long secret) => GrobValue.FromStruct(new GrobStruct(
        "token", [new KeyValuePair<string, GrobValue>("secret", GrobValue.FromInt(secret))]));

    /// <summary>
    /// A named type with one property, one method and a renderer that hides the field. The
    /// property's name is not a field of the struct and its value is derived, so a result
    /// can only have come through the catalogue's binding, never through field lookup.
    /// </summary>
    private static NamedTypeDefinition TokenType() => new(
        "token",
        [new NamedTypePropertyBinding(new PropertySignature("code", Int), s => GrobValue.FromInt(s.GetField("secret").AsInt() * 10))],
        [new NamedTypeMethodBinding(
            new FunctionSignature("twice", [], Int),
            s => new NativeFunction("twice", 0, (_, _) => GrobValue.FromInt(s.GetField("secret").AsInt() * 2)))],
        v => "<token>");

    private static NativeCatalogue WithToken() => Catalogue(new Plugin("tokens", r => r.RegisterNamedType(TokenType())));

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

    private static Chunk GetProperty(GrobValue receiver, string member, bool call = false) {
        var chunk = new Chunk();
        int receiverIdx = chunk.AddConstant(receiver);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte((byte)receiverIdx, 1);
        int memberIdx = chunk.AddConstant(GrobValue.FromString(member));
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte((byte)memberIdx, 1);
        if (call) {
            chunk.WriteOpCode(OpCode.Call, 1);
            chunk.WriteByte(0, 1);
        }
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    private static Chunk Print(GrobValue value) {
        var chunk = new Chunk();
        int idx = chunk.AddConstant(value);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte((byte)idx, 1);
        chunk.WriteOpCode(OpCode.Print, 1);
        chunk.WriteOpCode(OpCode.Nil, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    // ----- Construction -----

    [Fact]
    public void Constructor_NullCatalogue_Throws() {
        var ex = Assert.Throws<ArgumentNullException>(() => new VirtualMachine(new StringWriter(), (NativeCatalogue)null!));

        Assert.Equal("catalogue", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullStreamsWithCatalogue_Throws() {
        var ex = Assert.Throws<ArgumentNullException>(() => new VirtualMachine((IStandardStreams)null!, NativeCatalogue.Empty));

        Assert.Equal("streams", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithoutACatalogue_StartsFromTheEmptyOne() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);

        vm.Run(Print(Token(7)));

        Assert.Empty(vm.Globals);
        Assert.Equal($"token {{ secret: 7 }}{Environment.NewLine}", output.ToString());
    }

    // ----- Globals -----

    [Fact]
    public void CatalogueGlobals_AreBoundAndCallable() {
        var hello = new NativeFunction("greeting.hello", 0, (_, _) => GrobValue.FromString("hello"));
        NativeCatalogue catalogue = Catalogue(new Plugin("greeting", r => {
            r.RegisterNative(new FunctionSignature("greeting.hello", [], String), hello);
            r.RegisterConstant("greeting.answer", GrobValue.FromInt(42));
        }));
        var vm = new VirtualMachine(new StringWriter(), catalogue);

        vm.Run(CallGlobal("greeting.hello"));

        Assert.Same(hello, vm.Globals["greeting.hello"].AsFunction());
        Assert.Equal(GrobValue.FromInt(42), vm.Globals["greeting.answer"]);
        Assert.Equal(GrobValue.FromString("hello"), vm.Stack.Peek());
    }

    [Fact]
    public void CatalogueGlobals_AreCopied_NotShared() {
        NativeCatalogue catalogue = Catalogue(new Plugin("greeting", r => r.RegisterConstant("greeting.answer", GrobValue.FromInt(42))));
        var first = new VirtualMachine(new StringWriter(), catalogue);
        var second = new VirtualMachine(new StringWriter(), catalogue);

        first.RegisterConstant("greeting.answer", GrobValue.FromInt(43));

        Assert.Equal(GrobValue.FromInt(42), second.Globals["greeting.answer"]);
        Assert.Equal(GrobValue.FromInt(42), catalogue.Globals["greeting.answer"]);
    }

    // ----- Renderers -----

    [Fact]
    public void Print_NamedTypeWithRenderer_RendersThroughTheCatalogue() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output, WithToken());

        vm.Run(Print(Token(7)));

        Assert.Equal($"<token>{Environment.NewLine}", output.ToString());
        Assert.DoesNotContain("secret", output.ToString());
    }

    // ----- Named-type dispatch -----

    [Fact]
    public void GetProperty_NamedTypeProperty_DispatchesThroughTheCatalogue() {
        var vm = new VirtualMachine(new StringWriter(), WithToken());

        vm.Run(GetProperty(Token(7), "code"));

        Assert.Equal(GrobValue.FromInt(70), vm.Stack.Peek());
    }

    [Fact]
    public void GetProperty_NamedTypeMethod_BindsThroughTheCatalogue() {
        var vm = new VirtualMachine(new StringWriter(), WithToken());

        vm.Run(GetProperty(Token(7), "twice", call: true));

        Assert.Equal(GrobValue.FromInt(14), vm.Stack.Peek());
    }

    [Fact]
    public void GetProperty_UnknownMemberOfANamedType_ThrowsInternal() {
        var vm = new VirtualMachine(new StringWriter(), WithToken());

        var ex = Assert.Throws<GrobInternalException>(() => vm.Run(GetProperty(Token(7), "nope")));

        Assert.Contains("token", ex.Message);
        Assert.Contains("'nope'", ex.Message);
    }

    [Fact]
    public void GetProperty_StructNotInTheCatalogue_FallsThroughToFieldLookup() {
        var vm = new VirtualMachine(new StringWriter(), NativeCatalogue.Empty);

        vm.Run(GetProperty(Token(7), "secret"));

        Assert.Equal(GrobValue.FromInt(7), vm.Stack.Peek());
    }

    [Fact]
    public void VirtualMachine_IsNotARegistrar() =>
        Assert.False(typeof(IPluginRegistrar).IsAssignableFrom(typeof(VirtualMachine)),
            "The VM reads one catalogue (D-426 Decision 2); a second registration path must not come back.");
}
