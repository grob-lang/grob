# Call Frames

Each function call creates a call frame. Frames are stored in a fixed-size array
— no heap allocation per call. Maximum depth: 256 frames. Exceeding this throws
`RuntimeError`.

```csharp
struct CallFrame
{
    public BytecodeFunction Function;
    public int InstructionPointer;
    public int StackBase;
}
```

Local variables are stack slots — array indexing, not dictionary lookup. The
compiler assigns each local a slot index at compile time.

See also: [Overview](Overview.md), [Instruction Set](Instruction-Set.md)
