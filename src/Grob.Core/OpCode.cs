namespace Grob.Core;

/// <summary>
/// The complete v1 Grob instruction set. Defined in full from Sprint 2 -
/// never grown incrementally (same discipline as <see cref="TokenKind"/>).
/// Authority: grob-v1-requirements.md SS3.3.
/// </summary>
public enum OpCode : byte {
    // -------------------------------------------------------------------------
    // Values
    // -------------------------------------------------------------------------

    /// <summary>Push constant from pool (1-byte pool index).</summary>
    Constant,

    /// <summary>Push constant from pool (2-byte big-endian pool index; pool greater than 256 entries).</summary>
    ConstantLong,

    /// <summary>Push nil.</summary>
    Nil,

    /// <summary>Push true.</summary>
    True,

    /// <summary>Push false.</summary>
    False,

    /// <summary>Discard the top-of-stack value.</summary>
    Pop,

    /// <summary>Discard N values from the stack (1-byte operand: count).</summary>
    PopN,

    // -------------------------------------------------------------------------
    // Arithmetic - typed (compiler selects based on type-checker annotations)
    // -------------------------------------------------------------------------

    /// <summary>int + int to int.</summary>
    AddInt,

    /// <summary>float + float to float.</summary>
    AddFloat,

    /// <summary>int - int to int.</summary>
    SubtractInt,

    /// <summary>float - float to float.</summary>
    SubtractFloat,

    /// <summary>int * int to int.</summary>
    MultiplyInt,

    /// <summary>float * float to float.</summary>
    MultiplyFloat,

    /// <summary>int / int to int (truncating: 7 / 2 to 3).</summary>
    DivideInt,

    /// <summary>float / float to float.</summary>
    DivideFloat,

    /// <summary>int % int to int.</summary>
    ModuloInt,

    /// <summary>float % float to float.</summary>
    ModuloFloat,

    /// <summary>Unary minus on int.</summary>
    NegateInt,

    /// <summary>Unary minus on float.</summary>
    NegateFloat,

    /// <summary>string + string to string.</summary>
    Concat,

    // -------------------------------------------------------------------------
    // Type promotion
    // -------------------------------------------------------------------------

    /// <summary>Promote int to float (implicit promotion in mixed arithmetic).</summary>
    IntToFloat,

    // -------------------------------------------------------------------------
    // Comparison
    // -------------------------------------------------------------------------

    /// <summary>== (type-agnostic: compiler ensures operands match).</summary>
    Equal,

    /// <summary>!= (type-agnostic).</summary>
    NotEqual,

    /// <summary>int less than int to bool.</summary>
    LessInt,

    /// <summary>float less than float to bool.</summary>
    LessFloat,

    /// <summary>string less than string to bool (ordinal).</summary>
    LessString,

    /// <summary>int greater than int to bool.</summary>
    GreaterInt,

    /// <summary>float greater than float to bool.</summary>
    GreaterFloat,

    /// <summary>string greater than string to bool (ordinal).</summary>
    GreaterString,

    /// <summary>int less than or equal to int to bool.</summary>
    LessEqualInt,

    /// <summary>float less than or equal to float to bool.</summary>
    LessEqualFloat,

    /// <summary>int greater than or equal to int to bool.</summary>
    GreaterEqualInt,

    /// <summary>float greater than or equal to float to bool.</summary>
    GreaterEqualFloat,

    // -------------------------------------------------------------------------
    // Logic
    // -------------------------------------------------------------------------

    /// <summary>Logical not (!). AND and OR use jump-based short-circuit, not dedicated opcodes.</summary>
    Not,

    // -------------------------------------------------------------------------
    // Variables
    // -------------------------------------------------------------------------

    /// <summary>Push local from stack slot (1-byte slot index).</summary>
    GetLocal,

    /// <summary>Store top-of-stack into stack slot (1-byte slot index).</summary>
    SetLocal,

    /// <summary>Push global by name index (1-byte index).</summary>
    GetGlobal,

    /// <summary>Store top-of-stack into global by name index (1-byte index).</summary>
    SetGlobal,

    /// <summary>Create a global binding (1-byte name index).</summary>
    DefineGlobal,

    // -------------------------------------------------------------------------
    // Upvalues (closures)
    // -------------------------------------------------------------------------

    /// <summary>Push captured variable (1-byte upvalue slot).</summary>
    GetUpvalue,

    /// <summary>Store top-of-stack into captured variable (1-byte upvalue slot).</summary>
    SetUpvalue,

    /// <summary>Move upvalue from stack to heap.</summary>
    CloseUpvalue,

    /// <summary>Create a closure object wrapping the function constant at the given pool index (1-byte).</summary>
    Closure,

    // -------------------------------------------------------------------------
    // Properties and fields
    // -------------------------------------------------------------------------

    /// <summary>Get named property from top-of-stack struct (1-byte name index).</summary>
    GetProperty,

    /// <summary>Set named property on top-of-stack struct (1-byte name index).</summary>
    SetProperty,

    // -------------------------------------------------------------------------
    // Array operations
    // -------------------------------------------------------------------------

    /// <summary>Create array from N stack values (1-byte count); pushes the new array.</summary>
    NewArray,

    /// <summary>array[index] - pop index, pop array, push element.</summary>
    GetIndex,

    /// <summary>array[index] = value.</summary>
    SetIndex,

    // -------------------------------------------------------------------------
    // Control flow
    // -------------------------------------------------------------------------

    /// <summary>Unconditional forward jump (2-byte big-endian offset).</summary>
    Jump,

    /// <summary>Conditional forward jump if top-of-stack is falsy (2-byte offset); pops the condition.</summary>
    JumpIfFalse,

    /// <summary>Conditional forward jump for OR short-circuit if top-of-stack is truthy (2-byte offset).</summary>
    JumpIfTrue,

    /// <summary>Unconditional backward jump (2-byte big-endian offset).</summary>
    Loop,

    // -------------------------------------------------------------------------
    // Functions
    // -------------------------------------------------------------------------

    /// <summary>Call function (1-byte arg count); expects function reference below the arguments on the stack.</summary>
    Call,

    /// <summary>Return from the current function; pops return value, restores call frame.</summary>
    Return,

    // -------------------------------------------------------------------------
    // Structs
    // -------------------------------------------------------------------------

    /// <summary>Create struct instance (1-byte type-index operand).</summary>
    NewStruct,

    /// <summary>Create anonymous struct (1-byte field-count operand).</summary>
    NewAnonStruct,

    // -------------------------------------------------------------------------
    // I/O
    // -------------------------------------------------------------------------

    /// <summary>Print top-of-stack to stdout with a trailing newline; pops the value.</summary>
    Print,

    /// <summary>Terminate the script with the int exit code on top of the stack; pops the code (D-110).</summary>
    Exit,

    // -------------------------------------------------------------------------
    // Increment / decrement
    // -------------------------------------------------------------------------

    /// <summary>++ on int local (1-byte slot index).</summary>
    IncrementInt,

    /// <summary>-- on int local (1-byte slot index).</summary>
    DecrementInt,

    /// <summary>++ on float local (1-byte slot index).</summary>
    IncrementFloat,

    /// <summary>-- on float local (1-byte slot index).</summary>
    DecrementFloat,

    // -------------------------------------------------------------------------
    // Nil handling
    // -------------------------------------------------------------------------

    /// <summary>?? - pop two values; push the right operand if the left is nil, otherwise push the left.</summary>
    NilCoalesce,

    /// <summary>Push bool: is top-of-stack nil? Does not pop.</summary>
    IsNil,

    // -------------------------------------------------------------------------
    // String interpolation
    // -------------------------------------------------------------------------

    /// <summary>Concatenate N string fragments into one string (1-byte count); pops the fragments.</summary>
    BuildString,

    // -------------------------------------------------------------------------
    // Exception handling
    // -------------------------------------------------------------------------

    /// <summary>Mark start of a try block (1-byte handler-table index).</summary>
    TryBegin,

    /// <summary>Mark end of a try block.</summary>
    TryEnd,

    /// <summary>Throw the exception on top-of-stack.</summary>
    Throw,

    // -------------------------------------------------------------------------
    // Module
    // -------------------------------------------------------------------------

    /// <summary>Load a plugin module (1-byte name index).</summary>
    Import,
}
