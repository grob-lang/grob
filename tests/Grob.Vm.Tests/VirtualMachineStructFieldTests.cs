using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch tests for Sprint 6 Increment C — <see cref="OpCode.GetProperty"/>
/// and <see cref="OpCode.SetProperty"/> on struct values. Also covers D-325:
/// a closure stored in a struct field that escapes its enclosing frame must
/// correctly close its upvalue and be callable through the field post-return.
/// All chunks are hand-constructed; no compiler dependency.
/// </summary>
public sealed class VirtualMachineStructFieldTests {
    private static (VirtualMachine Vm, StringWriter Output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    // -----------------------------------------------------------------------
    // GetProperty — reads an existing field by name
    // -----------------------------------------------------------------------

    [Fact]
    public void GetProperty_ReadsExistingStringField() {
        var chunk = new Chunk();
        byte typeIdx = chunk.AddStructType(new StructTypeDescriptor("Config", ["host", "port"]));
        byte hostConst = (byte)chunk.AddConstant(GrobValue.FromString("example.com"));
        byte portConst = (byte)chunk.AddConstant(GrobValue.FromInt(8080));
        byte hostNameIdx = (byte)chunk.AddConstant(GrobValue.FromString("host"));

        // Build Config { host: "example.com", port: 8080 }
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(hostConst, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(portConst, 1);
        chunk.WriteOpCode(OpCode.NewStruct, 1); chunk.WriteByte(typeIdx, 1);
        // GetProperty "host"
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(hostNameIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.True(vm.Stack.Peek().IsString);
        Assert.Equal("example.com", vm.Stack.Peek().AsString());
    }

    // -----------------------------------------------------------------------
    // SetProperty — writes a field value and the change is visible via GetProperty
    // -----------------------------------------------------------------------

    [Fact]
    public void SetProperty_WritesField_ValueVisible() {
        var chunk = new Chunk();
        byte typeIdx = chunk.AddStructType(new StructTypeDescriptor("Config", ["host"]));
        byte orig = (byte)chunk.AddConstant(GrobValue.FromString("original"));
        byte updated = (byte)chunk.AddConstant(GrobValue.FromString("updated"));
        byte hostNameIdx = (byte)chunk.AddConstant(GrobValue.FromString("host"));

        // slot 0 := Config { host: "original" }
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(orig, 1);
        chunk.WriteOpCode(OpCode.NewStruct, 1); chunk.WriteByte(typeIdx, 1); // slot 0

        // c.host = "updated"  →  GetLocal 0, Constant "updated", SetProperty "host"
        chunk.WriteOpCode(OpCode.GetLocal, 1); chunk.WriteByte(0, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(updated, 1);
        chunk.WriteOpCode(OpCode.SetProperty, 1); chunk.WriteByte(hostNameIdx, 1);

        // Read it back: GetLocal 0, GetProperty "host"
        chunk.WriteOpCode(OpCode.GetLocal, 1); chunk.WriteByte(0, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(hostNameIdx, 1);

        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().IsString);
        Assert.Equal("updated", vm.Stack.Peek().AsString());
    }

    [Fact]
    public void SetProperty_LeavesReceiverOnStackUnchanged() {
        // SetProperty is a statement-level operation: pops both receiver and value,
        // pushes nothing. After SetProperty, the struct remains at its original slot.
        var chunk = new Chunk();
        byte typeIdx = chunk.AddStructType(new StructTypeDescriptor("Box", ["value"]));
        byte initialConst = (byte)chunk.AddConstant(GrobValue.FromInt(1));
        byte newConst = (byte)chunk.AddConstant(GrobValue.FromInt(2));
        byte valueNameIdx = (byte)chunk.AddConstant(GrobValue.FromString("value"));

        // slot 0 := Box { value: 1 }
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(initialConst, 1);
        chunk.WriteOpCode(OpCode.NewStruct, 1); chunk.WriteByte(typeIdx, 1);

        int stackBefore = 1; // struct is slot 0

        // SetProperty: push receiver (GetLocal 0), push new value, SetProperty
        chunk.WriteOpCode(OpCode.GetLocal, 1); chunk.WriteByte(0, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(newConst, 1);
        chunk.WriteOpCode(OpCode.SetProperty, 1); chunk.WriteByte(valueNameIdx, 1);

        // SetProperty consumed both GetLocal and Constant (net −2 pushes, SetProperty adds 0).
        // Stack after SetProperty should still be just the struct at slot 0.
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(stackBefore, vm.Stack.Count);
    }

    // -----------------------------------------------------------------------
    // SetProperty negative contracts — defensive guards
    // -----------------------------------------------------------------------

    [Fact]
    public void SetProperty_NonStructReceiver_ThrowsInternalException() {
        // Nil (or any non-struct value) as the SetProperty receiver is a VM-level bug:
        // the type checker should have rejected the source. SetProperty throws
        // GrobInternalException for a non-struct receiver. (GetProperty differs: a nil
        // receiver there is a runtime E5201, so the two opcodes are not symmetric.)
        var chunk = new Chunk();
        chunk.AddStructType(new StructTypeDescriptor("Box", ["value"]));
        byte fieldNameIdx = (byte)chunk.AddConstant(GrobValue.FromString("value"));
        byte newVal = (byte)chunk.AddConstant(GrobValue.FromInt(99));

        // Push nil (not a struct) as the receiver, then a value, then SetProperty.
        chunk.WriteOpCode(OpCode.Nil, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(newVal, 1);
        chunk.WriteOpCode(OpCode.SetProperty, 1); chunk.WriteByte(fieldNameIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        Assert.Throws<GrobInternalException>(() => vm.Run(chunk));
    }

    [Fact]
    public void SetProperty_UnknownFieldName_ThrowsInternalException() {
        // SetProperty on a field name that the struct was not initialised with
        // is a VM-level contract violation (type checker should have rejected via E1002).
        // The VM must mirror GetProperty's defensive guard and throw rather than
        // silently extending the struct with a new key.
        var chunk = new Chunk();
        byte typeIdx = chunk.AddStructType(new StructTypeDescriptor("Box", ["value"]));
        byte initVal = (byte)chunk.AddConstant(GrobValue.FromInt(1));
        byte unknownFieldIdx = (byte)chunk.AddConstant(GrobValue.FromString("nonexistent"));
        byte newVal = (byte)chunk.AddConstant(GrobValue.FromInt(99));

        // slot 0 := Box { value: 1 }
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(initVal, 1);
        chunk.WriteOpCode(OpCode.NewStruct, 1); chunk.WriteByte(typeIdx, 1);

        // attempt: box.nonexistent = 99
        chunk.WriteOpCode(OpCode.GetLocal, 1); chunk.WriteByte(0, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(newVal, 1);
        chunk.WriteOpCode(OpCode.SetProperty, 1); chunk.WriteByte(unknownFieldIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        Assert.Throws<GrobInternalException>(() => vm.Run(chunk));
    }

    // -----------------------------------------------------------------------
    // D-325 — closure stored in a struct field escapes its enclosing frame
    // -----------------------------------------------------------------------

    /// <summary>
    /// A closure that captures a local variable is wrapped in a struct field and
    /// returned from the enclosing function. After the enclosing frame exits —
    /// triggering <c>CloseUpvaluesFrom</c> — the struct's callback field remains
    /// callable and reads the correct closed upvalue. No stack underflow occurs.
    ///
    /// Script shape:
    ///   box := makeBox()          ; Box { callback: fn() → 42 } at slot 0
    ///   fn_ref := box.callback    ; GetProperty "callback" at slot 1
    ///   result := fn_ref()        ; Call → 42, at slot 1 (replaces fn_ref)
    ///   Return
    ///
    /// makeBox body:
    ///   captured := 42            ; local at slot 0 of the factory frame
    ///   closure = Closure(lambda, captures [isLocal=1, slot=0])
    ///   NewStruct Box { callback: closure }
    ///   Return  →  CloseUpvaluesFrom closes slot 0 (captured), Box escapes
    ///
    /// Lambda body: GetUpvalue(0) → Return
    /// </summary>
    [Fact]
    public void ClosureInField_EscapeD325_ReadsCorrectUpvalue() {
        // Lambda: returns the captured value via GetUpvalue(0).
        var lambdaChunk = new Chunk();
        lambdaChunk.WriteOpCode(OpCode.GetUpvalue, 1); lambdaChunk.WriteByte(0, 1);
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        lambdaChunk.WriteOpCode(OpCode.Nil, 1);   // safety-net
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        var lambdaFn = new BytecodeFunction(string.Empty, 0, lambdaChunk, upvalueCount: 1);

        // makeBox(): pushes 42 at slot 0 (captured), wraps closure in Box, returns Box.
        var factoryChunk = new Chunk();
        byte boxTypeIdx = factoryChunk.AddStructType(new StructTypeDescriptor("Box", ["callback"]));
        byte fortyTwoIdx = (byte)factoryChunk.AddConstant(GrobValue.FromInt(42));
        byte lambdaIdx = (byte)factoryChunk.AddConstant(GrobValue.FromFunction(lambdaFn));

        // captured := 42  →  slot 0 of the factory frame
        factoryChunk.WriteOpCode(OpCode.Constant, 1); factoryChunk.WriteByte(fortyTwoIdx, 1);

        // Create closure capturing isLocal=1 at slot 0
        factoryChunk.WriteOpCode(OpCode.Closure, 1); factoryChunk.WriteByte(lambdaIdx, 1);
        factoryChunk.WriteByte(1, 1); // isLocal = 1
        factoryChunk.WriteByte(0, 1); // slot = 0 (captured)

        // NewStruct Box { callback: <closure> } — closure is the field value
        factoryChunk.WriteOpCode(OpCode.NewStruct, 1); factoryChunk.WriteByte(boxTypeIdx, 1);
        factoryChunk.WriteOpCode(OpCode.Return, 1);    // CloseUpvaluesFrom closes captured
        factoryChunk.WriteOpCode(OpCode.Nil, 1);       // safety-net
        factoryChunk.WriteOpCode(OpCode.Return, 1);
        var factoryFn = new BytecodeFunction("makeBox", 0, factoryChunk);

        // Script:
        //   box := makeBox()            ; Box at slot 0
        //   GetLocal 0, GetProperty "callback"   ; closure at slot 1
        //   Call 0                      ; result = 42 at slot 1
        //   Return
        var script = new Chunk();
        byte factoryIdx = (byte)script.AddConstant(GrobValue.FromFunction(factoryFn));
        byte callbackNameIdx = (byte)script.AddConstant(GrobValue.FromString("callback"));

        // Call makeBox() → Box at slot 0
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte(factoryIdx, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);

        // Get the closure from Box.callback
        script.WriteOpCode(OpCode.GetLocal, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.GetProperty, 1); script.WriteByte(callbackNameIdx, 1);

        // Call the closure
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);

        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        // Top of stack should be 42 — the closed upvalue read after frame exit.
        Assert.Equal(42L, vm.Stack.Peek().AsInt());
    }

    /// <summary>
    /// Two separate calls to a factory function produce structs whose callback
    /// closures hold independent upvalues. Calling each closure returns its own
    /// captured value without interference.
    ///
    /// makeGetter(n: int): Box — captures parameter n at slot 0.
    /// Script: box1 = makeGetter(10), box2 = makeGetter(20);
    ///   box1.callback() → 10, box2.callback() → 20.
    /// </summary>
    [Fact]
    public void ClosureInField_PerCallIndependence() {
        // Lambda body: GetUpvalue(0), Return.
        var lambdaChunk = new Chunk();
        lambdaChunk.WriteOpCode(OpCode.GetUpvalue, 1); lambdaChunk.WriteByte(0, 1);
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        lambdaChunk.WriteOpCode(OpCode.Nil, 1);
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        var lambdaFn = new BytecodeFunction(string.Empty, 0, lambdaChunk, upvalueCount: 1);

        // makeGetter(n: int): Box — n is at slot 0 (parameter).
        var getterChunk = new Chunk();
        byte boxTypeIdx = getterChunk.AddStructType(new StructTypeDescriptor("Box", ["callback"]));
        byte lambdaIdx = (byte)getterChunk.AddConstant(GrobValue.FromFunction(lambdaFn));

        // Closure capturing slot 0 (n)
        getterChunk.WriteOpCode(OpCode.Closure, 1); getterChunk.WriteByte(lambdaIdx, 1);
        getterChunk.WriteByte(1, 1); // isLocal = 1
        getterChunk.WriteByte(0, 1); // slot = 0 (n)

        getterChunk.WriteOpCode(OpCode.NewStruct, 1); getterChunk.WriteByte(boxTypeIdx, 1);
        getterChunk.WriteOpCode(OpCode.Return, 1);
        getterChunk.WriteOpCode(OpCode.Nil, 1);
        getterChunk.WriteOpCode(OpCode.Return, 1);
        var getterFn = new BytecodeFunction("makeGetter", 1, getterChunk); // 1 parameter

        // Script
        var script = new Chunk();
        byte getterIdx = (byte)script.AddConstant(GrobValue.FromFunction(getterFn));
        byte tenIdx = (byte)script.AddConstant(GrobValue.FromInt(10));
        byte twentyIdx = (byte)script.AddConstant(GrobValue.FromInt(20));
        byte callbackNameIdx = (byte)script.AddConstant(GrobValue.FromString("callback"));

        // box1 := makeGetter(10) at slot 0
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte(getterIdx, 1);
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte(tenIdx, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(1, 1);

        // box2 := makeGetter(20) at slot 1
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte(getterIdx, 1);
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte(twentyIdx, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(1, 1);

        // Call box1.callback() → 10, lands at slot 2
        script.WriteOpCode(OpCode.GetLocal, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.GetProperty, 1); script.WriteByte(callbackNameIdx, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);

        // Call box2.callback() → 20, lands at slot 3
        script.WriteOpCode(OpCode.GetLocal, 1); script.WriteByte(1, 1);
        script.WriteOpCode(OpCode.GetProperty, 1); script.WriteByte(callbackNameIdx, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);

        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        // Stack: [box1, box2, r1=10, r2=20] — Peek(0)=20, Peek(1)=10.
        Assert.Equal(20L, vm.Stack.Peek(0).AsInt());
        Assert.Equal(10L, vm.Stack.Peek(1).AsInt());
    }
}
