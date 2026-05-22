namespace Grob.Core;

/// <summary>
/// Identifies the syntactic category of a <see cref="Token"/>.
/// Every keyword, operator, punctuation mark, literal kind and structure token
/// that Grob v1 requires is defined here — completely, from the first commit.
/// See grob-v1-requirements.md §3.4.
/// </summary>
public enum TokenKind {
    // -------------------------------------------------------------------------
    // Keywords (§3.4)
    // -------------------------------------------------------------------------

    /// <summary>fn</summary>
    Fn,

    /// <summary>if</summary>
    If,

    /// <summary>else</summary>
    Else,

    /// <summary>while</summary>
    While,

    /// <summary>for</summary>
    For,

    /// <summary>in</summary>
    In,

    /// <summary>return</summary>
    Return,

    /// <summary>const</summary>
    Const,

    /// <summary>readonly</summary>
    Readonly,

    /// <summary>type</summary>
    Type,

    /// <summary>param</summary>
    Param,

    /// <summary>import</summary>
    Import,

    /// <summary>as</summary>
    As,

    /// <summary>try</summary>
    Try,

    /// <summary>catch</summary>
    Catch,

    /// <summary>finally</summary>
    Finally,

    /// <summary>throw</summary>
    Throw,

    /// <summary>select</summary>
    Select,

    /// <summary>case</summary>
    Case,

    /// <summary>default</summary>
    Default,

    /// <summary>break</summary>
    Break,

    /// <summary>continue</summary>
    Continue,

    /// <summary>true</summary>
    True,

    /// <summary>false</summary>
    False,

    /// <summary>nil</summary>
    Nil,

    /// <summary>step</summary>
    Step,

    /// <summary>switch</summary>
    Switch,

    // -------------------------------------------------------------------------
    // Operators (§3.4)
    // -------------------------------------------------------------------------

    /// <summary>+</summary>
    Plus,

    /// <summary>-</summary>
    Minus,

    /// <summary>*</summary>
    Star,

    /// <summary>/</summary>
    Slash,

    /// <summary>%</summary>
    Percent,

    /// <summary>=</summary>
    Assign,

    /// <summary>:=</summary>
    ColonAssign,

    /// <summary>==</summary>
    EqualEqual,

    /// <summary>!=</summary>
    BangEqual,

    /// <summary>&lt;</summary>
    Less,

    /// <summary>&gt;</summary>
    Greater,

    /// <summary>&lt;=</summary>
    LessEqual,

    /// <summary>&gt;=</summary>
    GreaterEqual,

    /// <summary>!</summary>
    Bang,

    /// <summary>&amp;&amp;</summary>
    AmpAmp,

    /// <summary>||</summary>
    PipePipe,

    /// <summary>?</summary>
    Question,

    /// <summary>:</summary>
    Colon,

    /// <summary>??</summary>
    QuestionQuestion,

    /// <summary>?.</summary>
    QuestionDot,

    /// <summary>+=</summary>
    PlusAssign,

    /// <summary>-=</summary>
    MinusAssign,

    /// <summary>*=</summary>
    StarAssign,

    /// <summary>/=</summary>
    SlashAssign,

    /// <summary>%=</summary>
    PercentAssign,

    /// <summary>++</summary>
    PlusPlus,

    /// <summary>--</summary>
    MinusMinus,

    /// <summary>..</summary>
    DotDot,

    /// <summary>=&gt;</summary>
    Arrow,

    // -------------------------------------------------------------------------
    // Punctuation (§3.4)
    // -------------------------------------------------------------------------

    /// <summary>(</summary>
    LeftParen,

    /// <summary>)</summary>
    RightParen,

    /// <summary>{</summary>
    LeftBrace,

    /// <summary>}</summary>
    RightBrace,

    /// <summary>[</summary>
    LeftBracket,

    /// <summary>]</summary>
    RightBracket,

    /// <summary>,</summary>
    Comma,

    /// <summary>.</summary>
    Dot,

    /// <summary>#{  — anonymous-struct / map literal opener</summary>
    HashBrace,

    /// <summary>///  — doc-comment line; lexer recognises and discards in v1</summary>
    DocComment,

    // -------------------------------------------------------------------------
    // Decorators (§3.4)
    // -------------------------------------------------------------------------

    /// <summary>@  — decorator prefix (@secure, @allowed, @minLength, @maxLength)</summary>
    At,

    // -------------------------------------------------------------------------
    // Literals (§3.4)
    // -------------------------------------------------------------------------

    /// <summary>Integer literal (decimal, hex 0x…, binary 0b…, with optional _ separators)</summary>
    IntLiteral,

    /// <summary>Floating-point literal</summary>
    FloatLiteral,

    /// <summary>Opening " of an interpolated string</summary>
    StringStart,

    /// <summary>A raw-text segment inside an interpolated string</summary>
    StringPart,

    /// <summary>Closing " of an interpolated string</summary>
    StringEnd,

    /// <summary>${ — start of an interpolated expression inside a string</summary>
    InterpStart,

    /// <summary>} — end of an interpolated expression inside a string</summary>
    InterpEnd,

    /// <summary>Backtick raw string literal</summary>
    RawStringLiteral,

    /// <summary>Triple-backtick raw block string literal</summary>
    RawStringBlockLiteral,

    /// <summary>Regex literal (r"…")</summary>
    RegexLiteral,

    /// <summary>Identifier (includes built-in names print, exit, input)</summary>
    Identifier,

    // -------------------------------------------------------------------------
    // Structure (§3.4)
    // -------------------------------------------------------------------------

    /// <summary>
    /// A significant newline — a line break that acts as a statement boundary.
    /// The lexer only emits this outside open brackets (<see cref="Token.BracketDepth"/> == 0)
    /// and after a non-continuation token.
    /// </summary>
    Newline,

    /// <summary>End of the token stream</summary>
    Eof,

    /// <summary>
    /// An unrecognised character or malformed literal. The lexer emits one
    /// Error token per bad character and continues scanning.
    /// </summary>
    Error,
}
