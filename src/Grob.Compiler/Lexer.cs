using System.Globalization;
using System.Text;

using Grob.Core;

namespace Grob.Compiler;

/// <summary>
/// The Grob v1 lexer. Single-pass scan over source text producing
/// <see cref="Token"/>s and writing diagnostics into a <see cref="DiagnosticBag"/>.
///
/// The lexer never throws on malformed input — it diagnoses, emits a recovery
/// token where useful, and continues so that as many errors as possible surface
/// from a single run. See sprint 1 increment B and
/// <c>docs/design/grob-language-fundamentals.md</c> §8 (literals) and §14 (line
/// continuation) for the authoritative behaviour.
/// </summary>
public sealed class Lexer {
    private readonly string _source;
    private readonly string _file;
    private readonly DiagnosticBag _diagnostics;
    private readonly List<Token> _rawTokens = [];

    // Cursor — absolute index plus 1-based line/column of the character at _pos.
    private int _pos;
    private int _line = 1;
    private int _col = 1;

    // Open-bracket nesting depth: count of currently-open ( [ { #{ ${ pairs.
    // Tokens are stamped with the depth *outside* their own pair, so a matching
    // ( and ) share the same depth.
    private int _depth;

    // Pending interpolations: each frame records the bracket depth that was in
    // effect *before* the corresponding "${" was consumed. When the lexer is
    // about to emit a closing "}" at depth = frame.OuterDepth + 1, that brace is
    // an InterpEnd and the lexer must resume scanning the enclosing string.
    private readonly Stack<InterpFrame> _interpStack = new();

    private Lexer(string source, string file, DiagnosticBag diagnostics) {
        _source = source;
        _file = file;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Scans <paramref name="source"/> into a token stream.
    /// </summary>
    /// <param name="source">The source text. Must not be null.</param>
    /// <param name="diagnostics">The diagnostic sink. Must not be null.</param>
    /// <param name="file">Opaque file identifier recorded on each token. Defaults to <c>&lt;source&gt;</c>.</param>
    /// <returns>The full token stream, ending with a single <see cref="TokenKind.Eof"/> at depth zero.</returns>
    public static IReadOnlyList<Token> Scan(string source, DiagnosticBag diagnostics, string file = "<source>") {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(file);
        return new Lexer(source, file, diagnostics).ScanAll();
    }

    private IReadOnlyList<Token> ScanAll() {
        while (!IsAtEnd) {
            ScanToken();
        }
        Emit(TokenKind.Eof, string.Empty, Here(), depthOverride: 0);
        return ApplyLineContinuation(_rawTokens);
    }

    // ---------------------------------------------------------------------
    // Main dispatch
    // ---------------------------------------------------------------------

    private void ScanToken() {
        char c = Peek();
        switch (c) {
            case ' ':
            case '\t':
            case '\r':
                Advance();
                return;
            case '\n':
                EmitFixed(TokenKind.Newline, string.Empty);
                AdvanceNewline();
                return;
            case '(': EmitFixed(TokenKind.LeftParen, "("); _depth++; Advance(); return;
            case ')': ScanCloser(TokenKind.RightParen, ")"); return;
            case '[': EmitFixed(TokenKind.LeftBracket, "["); _depth++; Advance(); return;
            case ']': ScanCloser(TokenKind.RightBracket, "]"); return;
            case '{': EmitFixed(TokenKind.LeftBrace, "{"); _depth++; Advance(); return;
            case '}': ScanCloseBrace(); return;
            case ',': EmitFixed(TokenKind.Comma, ","); Advance(); return;
            case '@': EmitFixed(TokenKind.At, "@"); Advance(); return;
            case '.': ScanDot(); return;
            case ':': ScanColon(); return;
            case ';':
                ErrorChar(c, "E1004", "semicolons are not used in Grob — newlines terminate statements");
                return;
            case '#': ScanHash(); return;
            case '"': ScanInterpolatedString(); return;
            case '`': ScanRawString(); return;
            case '/': ScanSlash(); return;
            case '+': ScanRepeatedOrAssign('+', TokenKind.Plus, TokenKind.PlusPlus, TokenKind.PlusAssign); return;
            case '-': ScanRepeatedOrAssign('-', TokenKind.Minus, TokenKind.MinusMinus, TokenKind.MinusAssign); return;
            case '*': ScanSingleOrAssign('*', TokenKind.Star, TokenKind.StarAssign); return;
            case '%': ScanSingleOrAssign('%', TokenKind.Percent, TokenKind.PercentAssign); return;
            case '=': ScanEquals(); return;
            case '!': ScanBang(); return;
            case '<': ScanLess(); return;
            case '>': ScanGreater(); return;
            case '?': ScanQuestion(); return;
            case '&': ScanAmpOrPipe('&', TokenKind.AmpAmp); return;
            case '|': ScanAmpOrPipe('|', TokenKind.PipePipe); return;
            default:
                if (IsDigit(c)) { ScanNumber(); return; }
                if (IsIdentStart(c)) { ScanIdentifier(); return; }
                ErrorChar(c, "E1001", $"unexpected character '{Describe(c)}'");
                return;
        }
    }

    // ---------------------------------------------------------------------
    // Punctuation that has multi-character variants
    // ---------------------------------------------------------------------

    private void ScanDot() {
        if (PeekAt(1) == '.') {
            EmitFixed(TokenKind.DotDot, "..");
            Advance(2);
            return;
        }
        EmitFixed(TokenKind.Dot, ".");
        Advance();
    }

    private void ScanColon() {
        if (PeekAt(1) == '=') {
            EmitFixed(TokenKind.ColonAssign, ":=");
            Advance(2);
            return;
        }
        EmitFixed(TokenKind.Colon, ":");
        Advance();
    }

    private void ScanHash() {
        if (PeekAt(1) == '{') {
            EmitFixed(TokenKind.HashBrace, "#{");
            _depth++;
            Advance(2);
            return;
        }
        ErrorChar('#', "E1002", "'#' must be followed by '{' to open an anonymous-struct literal");
    }

    private void ScanEquals() {
        char next = PeekAt(1);
        if (next == '=') {
            EmitFixed(TokenKind.EqualEqual, "==");
            Advance(2);
            return;
        }
        if (next == '>') {
            EmitFixed(TokenKind.Arrow, "=>");
            Advance(2);
            return;
        }
        EmitFixed(TokenKind.Assign, "=");
        Advance();
    }

    private void ScanBang() {
        if (PeekAt(1) == '=') {
            EmitFixed(TokenKind.BangEqual, "!=");
            Advance(2);
            return;
        }
        EmitFixed(TokenKind.Bang, "!");
        Advance();
    }

    private void ScanLess() {
        if (PeekAt(1) == '=') {
            EmitFixed(TokenKind.LessEqual, "<=");
            Advance(2);
            return;
        }
        EmitFixed(TokenKind.Less, "<");
        Advance();
    }

    private void ScanGreater() {
        if (PeekAt(1) == '=') {
            EmitFixed(TokenKind.GreaterEqual, ">=");
            Advance(2);
            return;
        }
        EmitFixed(TokenKind.Greater, ">");
        Advance();
    }

    private void ScanQuestion() {
        char next = PeekAt(1);
        if (next == '?') {
            EmitFixed(TokenKind.QuestionQuestion, "??");
            Advance(2);
            return;
        }
        if (next == '.') {
            EmitFixed(TokenKind.QuestionDot, "?.");
            Advance(2);
            return;
        }
        EmitFixed(TokenKind.Question, "?");
        Advance();
    }

    private void ScanSingleOrAssign(char self, TokenKind single, TokenKind withAssign) {
        if (PeekAt(1) == '=') {
            EmitFixed(withAssign, $"{self}=");
            Advance(2);
            return;
        }
        EmitFixed(single, self.ToString());
        Advance();
    }

    private void ScanRepeatedOrAssign(char self, TokenKind single, TokenKind doubled, TokenKind withAssign) {
        char next = PeekAt(1);
        if (next == self) {
            EmitFixed(doubled, $"{self}{self}");
            Advance(2);
            return;
        }
        if (next == '=') {
            EmitFixed(withAssign, $"{self}=");
            Advance(2);
            return;
        }
        EmitFixed(single, self.ToString());
        Advance();
    }

    private void ScanAmpOrPipe(char self, TokenKind doubled) {
        if (PeekAt(1) == self) {
            EmitFixed(doubled, $"{self}{self}");
            Advance(2);
            return;
        }
        ErrorChar(self, "E1003", $"single '{self}' is not a Grob operator (did you mean '{self}{self}'?)");
    }

    private void ScanCloseBrace() {
        // If we're inside an interpolation and this brace closes it, it's an
        // InterpEnd token and we must resume scanning the surrounding string.
        if (_interpStack.TryPeek(out InterpFrame frame) && _depth == frame.OuterDepth + 1) {
            _depth--;
            EmitFixed(TokenKind.InterpEnd, "}");
            Advance();
            _interpStack.Pop();
            ContinueInterpolatedString();
            return;
        }
        ScanCloser(TokenKind.RightBrace, "}");
    }

    private void ScanCloser(TokenKind kind, string lexeme) {
        if (_depth == 0) {
            SourceLocation loc = Here();
            AddError("E1005", $"unbalanced '{lexeme}'", loc);
            Emit(TokenKind.Error, lexeme, loc, depthOverride: 0);
            Advance();
            return;
        }
        _depth--;
        EmitFixed(kind, lexeme);
        Advance();
    }

    // ---------------------------------------------------------------------
    // Slash — comments, regex, division
    // ---------------------------------------------------------------------

    private void ScanSlash() {
        char p1 = PeekAt(1);
        char p2 = PeekAt(2);
        if (p1 == '/' && p2 == '/') {
            // /// — doc comment line. Recognised then discarded in v1.
            ConsumeLineComment();
            return;
        }
        if (p1 == '/') {
            ConsumeLineComment();
            return;
        }
        if (p1 == '*') {
            ConsumeBlockComment();
            return;
        }
        if (p1 == '=') {
            EmitFixed(TokenKind.SlashAssign, "/=");
            Advance(2);
            return;
        }
        if (CanStartRegex()) {
            ScanRegexLiteral();
            return;
        }
        EmitFixed(TokenKind.Slash, "/");
        Advance();
    }

    private void ConsumeLineComment() {
        // Consume until newline (exclusive); the newline is emitted on the next
        // outer iteration. No token is produced.
        while (!IsAtEnd && Peek() != '\n') {
            Advance();
        }
    }

    private void ConsumeBlockComment() {
        SourceLocation start = Here();
        Advance(2);  // skip /*
        while (!IsAtEnd) {
            if (Peek() == '*' && PeekAt(1) == '/') {
                Advance(2);
                return;
            }
            if (Peek() == '\n') {
                AdvanceNewline();
            } else {
                Advance();
            }
        }
        AddError("E1005", "unterminated block comment", start);
    }

    private bool CanStartRegex() {
        // A regex literal is admissible when the previous emitted, non-newline
        // token cannot itself end an expression. Newlines do not change the
        // expression context — we look through them to the last real token.
        for (int i = _rawTokens.Count - 1; i >= 0; i--) {
            TokenKind kind = _rawTokens[i].Kind;
            if (kind == TokenKind.Newline) continue;
            return RegexAdmissibleAfter(kind);
        }
        return true;  // start of file
    }

    private static bool RegexAdmissibleAfter(TokenKind kind) =>
        kind switch {
            // Anything that can syntactically terminate an expression: regex is NOT admissible.
            TokenKind.Identifier => false,
            TokenKind.IntLiteral => false,
            TokenKind.FloatLiteral => false,
            TokenKind.StringEnd => false,
            TokenKind.RawStringLiteral => false,
            TokenKind.RawStringBlockLiteral => false,
            TokenKind.RegexLiteral => false,
            TokenKind.True => false,
            TokenKind.False => false,
            TokenKind.Nil => false,
            TokenKind.RightParen => false,
            TokenKind.RightBracket => false,
            TokenKind.RightBrace => false,
            TokenKind.PlusPlus => false,
            TokenKind.MinusMinus => false,
            _ => true,
        };

    private void ScanRegexLiteral() {
        SourceLocation start = Here();
        var sb = new StringBuilder();
        sb.Append('/');
        Advance();  // opening /
        while (!IsAtEnd) {
            char c = Peek();
            if (c == '\n') {
                AddError("E1006", "unterminated regex literal", start);
                Emit(TokenKind.Error, sb.ToString(), start);
                return;
            }
            if (c == '\\') {
                sb.Append(c);
                Advance();
                if (!IsAtEnd && Peek() != '\n') {
                    sb.Append(Peek());
                    Advance();
                }
                continue;
            }
            if (c == '/') {
                sb.Append(c);
                Advance();
                // Optional flags — letters following the closing slash.
                while (!IsAtEnd && IsIdentContinue(Peek()) && char.IsLetter(Peek())) {
                    sb.Append(Peek());
                    Advance();
                }
                Emit(TokenKind.RegexLiteral, sb.ToString(), start);
                return;
            }
            sb.Append(c);
            Advance();
        }
        AddError("E1006", "unterminated regex literal", start);
        Emit(TokenKind.Error, sb.ToString(), start);
    }

    // ---------------------------------------------------------------------
    // Numbers — int decimal/hex/binary with _ separators, and floats
    // ---------------------------------------------------------------------

    private void ScanNumber() {
        SourceLocation start = Here();
        var sb = new StringBuilder();
        char c = Peek();

        if (c == '0' && (PeekAt(1) == 'x' || PeekAt(1) == 'X')) {
            sb.Append(Peek()); Advance();
            sb.Append(Peek()); Advance();
            int digits = ConsumeDigitsInto(sb, IsHexDigit);
            if (digits == 0) {
                AddError("E1010", "hexadecimal literal has no digits", start);
            }
            Emit(TokenKind.IntLiteral, sb.ToString(), start);
            return;
        }

        if (c == '0' && (PeekAt(1) == 'b' || PeekAt(1) == 'B')) {
            sb.Append(Peek()); Advance();
            sb.Append(Peek()); Advance();
            int digits = ConsumeDigitsInto(sb, IsBinaryDigit);
            if (digits == 0) {
                AddError("E1011", "binary literal has no digits", start);
            }
            Emit(TokenKind.IntLiteral, sb.ToString(), start);
            return;
        }

        ConsumeDigitsInto(sb, IsDecimalDigit);

        // Float: '.' followed by a digit. A trailing dot without a fractional
        // digit is a range operator or member access — leave it for the next pass.
        if (Peek() == '.' && IsDigit(PeekAt(1))) {
            sb.Append('.');
            Advance();
            ConsumeDigitsInto(sb, IsDecimalDigit);
            Emit(TokenKind.FloatLiteral, sb.ToString(), start);
            return;
        }

        Emit(TokenKind.IntLiteral, sb.ToString(), start);
    }

    private int ConsumeDigitsInto(StringBuilder sb, Func<char, bool> isDigit) {
        int digitCount = 0;
        while (!IsAtEnd) {
            char c = Peek();
            if (isDigit(c)) {
                sb.Append(c);
                Advance();
                digitCount++;
                continue;
            }
            if (c == '_') {
                sb.Append(c);
                Advance();
                continue;
            }
            break;
        }
        return digitCount;
    }

    private static bool IsDecimalDigit(char c) => c >= '0' && c <= '9';
    private static bool IsHexDigit(char c) => IsDecimalDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    private static bool IsBinaryDigit(char c) => c == '0' || c == '1';

    // ---------------------------------------------------------------------
    // Identifiers and keywords
    // ---------------------------------------------------------------------

    private void ScanIdentifier() {
        SourceLocation start = Here();
        int begin = _pos;
        while (!IsAtEnd && IsIdentContinue(Peek())) {
            Advance();
        }
        string text = _source.Substring(begin, _pos - begin);
        TokenKind kind = LookupKeyword(text);
        Emit(kind, text, start);
    }

    private static bool IsIdentStart(char c) => c == '_' || char.IsLetter(c);
    private static bool IsIdentContinue(char c) => c == '_' || char.IsLetterOrDigit(c);
    private static bool IsDigit(char c) => c >= '0' && c <= '9';

    private static TokenKind LookupKeyword(string text) =>
        text switch {
            "fn" => TokenKind.Fn,
            "if" => TokenKind.If,
            "else" => TokenKind.Else,
            "while" => TokenKind.While,
            "for" => TokenKind.For,
            "in" => TokenKind.In,
            "return" => TokenKind.Return,
            "const" => TokenKind.Const,
            "readonly" => TokenKind.Readonly,
            "type" => TokenKind.Type,
            "param" => TokenKind.Param,
            "import" => TokenKind.Import,
            "as" => TokenKind.As,
            "try" => TokenKind.Try,
            "catch" => TokenKind.Catch,
            "finally" => TokenKind.Finally,
            "throw" => TokenKind.Throw,
            "select" => TokenKind.Select,
            "case" => TokenKind.Case,
            "default" => TokenKind.Default,
            "break" => TokenKind.Break,
            "continue" => TokenKind.Continue,
            "true" => TokenKind.True,
            "false" => TokenKind.False,
            "nil" => TokenKind.Nil,
            "step" => TokenKind.Step,
            "switch" => TokenKind.Switch,
            _ => TokenKind.Identifier,
        };

    // ---------------------------------------------------------------------
    // Strings — double-quoted with ${...} interpolation
    // ---------------------------------------------------------------------

    private void ScanInterpolatedString() {
        SourceLocation start = Here();
        Emit(TokenKind.StringStart, "\"", start);
        Advance();  // consume opening "
        ContinueInterpolatedString();
    }

    private void ContinueInterpolatedString() {
        SourceLocation partStart = Here();
        var sb = new StringBuilder();
        while (!IsAtEnd) {
            char c = Peek();
            if (c == '"') {
                FlushStringPart(sb, partStart);
                Emit(TokenKind.StringEnd, "\"", Here());
                Advance();
                return;
            }
            if (c == '\n') {
                CloseUnterminatedStringAtNewline(sb, partStart);
                return;
            }
            if (c == '\\') {
                ConsumeStringEscape(sb);
                continue;
            }
            if (c == '$' && PeekAt(1) == '{') {
                OpenInterpolation(sb, partStart);
                return;
            }
            sb.Append(c);
            Advance();
        }
        FlushStringPart(sb, partStart);
        AddError("E1022", "unterminated string literal", partStart);
        Emit(TokenKind.StringEnd, string.Empty, Here());
    }

    private void CloseUnterminatedStringAtNewline(StringBuilder sb, SourceLocation partStart) {
        FlushStringPart(sb, partStart);
        AddError("E1020", "unterminated string literal — strings cannot span lines", partStart);
        // Synthesise a closing StringEnd at the newline so the segmentation
        // remains well-formed for the parser. The newline itself is
        // emitted on the next outer iteration.
        Emit(TokenKind.StringEnd, string.Empty, Here());
    }

    private void ConsumeStringEscape(StringBuilder sb) {
        // Validate the escape — but keep the raw two-character source in
        // the StringPart lexeme. The compiler decodes escapes later.
        char esc = PeekAt(1);
        if (!IsValidEscape(esc)) {
            AddError("E1021", $"invalid escape sequence '\\{Describe(esc)}'", Here());
        }
        sb.Append('\\');
        Advance();
        if (!IsAtEnd && Peek() != '\n') {
            sb.Append(Peek());
            Advance();
        }
    }

    private void OpenInterpolation(StringBuilder sb, SourceLocation partStart) {
        FlushStringPart(sb, partStart);
        SourceLocation interpStart = Here();
        Emit(TokenKind.InterpStart, "${", interpStart);
        _interpStack.Push(new InterpFrame(_depth));
        _depth++;
        Advance(2);
    }

    private void FlushStringPart(StringBuilder sb, SourceLocation start) {
        if (sb.Length == 0) return;
        Emit(TokenKind.StringPart, sb.ToString(), start);
        sb.Clear();
    }

    private static bool IsValidEscape(char c) =>
        c is 'n' or 'r' or 't' or '\\' or '"' or '$';

    // ---------------------------------------------------------------------
    // Raw backtick strings — single-line and triple-backtick block form
    // ---------------------------------------------------------------------

    private void ScanRawString() {
        SourceLocation start = Here();
        if (PeekAt(1) == '`' && PeekAt(2) == '`') {
            ScanRawBlock(start);
            return;
        }
        ScanRawSingle(start);
    }

    private void ScanRawSingle(SourceLocation start) {
        var sb = new StringBuilder();
        sb.Append('`');
        Advance();  // opening `
        while (!IsAtEnd) {
            char c = Peek();
            if (c == '\n') {
                AddError("E1030", "unterminated raw string — single-backtick strings cannot span lines", start);
                Emit(TokenKind.Error, sb.ToString(), start);
                return;
            }
            if (c == '`') {
                sb.Append(c);
                Advance();
                Emit(TokenKind.RawStringLiteral, sb.ToString(), start);
                return;
            }
            sb.Append(c);
            Advance();
        }
        AddError("E1030", "unterminated raw string", start);
        Emit(TokenKind.Error, sb.ToString(), start);
    }

    private void ScanRawBlock(SourceLocation start) {
        var sb = new StringBuilder();
        sb.Append("```");
        Advance(3);
        while (!IsAtEnd) {
            if (Peek() == '`' && PeekAt(1) == '`' && PeekAt(2) == '`') {
                sb.Append("```");
                Advance(3);
                Emit(TokenKind.RawStringBlockLiteral, sb.ToString(), start);
                return;
            }
            if (Peek() == '\n') {
                sb.Append('\n');
                AdvanceNewline();
                continue;
            }
            sb.Append(Peek());
            Advance();
        }
        AddError("E1031", "unterminated triple-backtick raw block string", start);
        Emit(TokenKind.Error, sb.ToString(), start);
    }

    // ---------------------------------------------------------------------
    // Token emission and cursor management
    // ---------------------------------------------------------------------

    private void EmitFixed(TokenKind kind, string lexeme) => Emit(kind, lexeme, Here());

    private void Emit(TokenKind kind, string lexeme, SourceLocation location, int? depthOverride = null) {
        int depth = depthOverride ?? _depth;
        _rawTokens.Add(new Token(kind, lexeme, location, depth));
    }

    private void ErrorChar(char c, string code, string message) {
        SourceLocation loc = Here();
        AddError(code, message, loc);
        Emit(TokenKind.Error, c.ToString(CultureInfo.InvariantCulture), loc);
        Advance();
    }

    private void AddError(string code, string message, SourceLocation at) =>
        _diagnostics.Add(new Diagnostic(code, message, new SourceRange(at), Severity.Error));

    private SourceLocation Here() => new(_file, _line, _col);

    private bool IsAtEnd => _pos >= _source.Length;

    private char Peek() => _pos < _source.Length ? _source[_pos] : '\0';

    private char PeekAt(int offset) {
        int i = _pos + offset;
        return (uint)i < (uint)_source.Length ? _source[i] : '\0';
    }

    private void Advance() {
        if (_pos < _source.Length) {
            _pos++;
            _col++;
        }
    }

    private void Advance(int n) {
        for (int i = 0; i < n; i++) Advance();
    }

    private void AdvanceNewline() {
        // Newlines: accept LF or CRLF. CR alone is treated as whitespace and
        // never triggers a Newline token (so Windows-style \r\n produces exactly
        // one Newline at the \n).
        if (_pos < _source.Length && _source[_pos] == '\n') {
            _pos++;
            _line++;
            _col = 1;
        }
    }

    private static string Describe(char c) =>
        c switch {
            '\0' => "<eof>",
            '\n' => "<newline>",
            '\r' => "<cr>",
            '\t' => "<tab>",
            _ => c.ToString(CultureInfo.InvariantCulture),
        };

    // ---------------------------------------------------------------------
    // Line continuation — post-pass over raw tokens
    // ---------------------------------------------------------------------

    private static List<Token> ApplyLineContinuation(List<Token> raw) {
        var result = new List<Token>(raw.Count);
        for (int i = 0; i < raw.Count; i++) {
            Token t = raw[i];
            if (t.Kind != TokenKind.Newline) {
                result.Add(t);
                continue;
            }
            // A newline inside any open bracket is irrelevant to the parser as
            // a statement boundary, but the prompt is explicit that BracketDepth
            // alone carries that information — the lexer still emits the token.
            // Line-continuation suppression follows §14: trailing token, or
            // next non-newline token is a leading-dot member access.
            Token? prevReal = FindPreviousReal(raw, i);
            Token? nextReal = FindNextReal(raw, i);
            if (prevReal is not null && IsContinuationEligible(prevReal.Kind)) {
                continue;
            }
            if (nextReal is { Kind: TokenKind.Dot }) {
                continue;
            }
            // Suppress newlines immediately before `)` or `]` — these close
            // expression-list constructs (arg lists, array literals) and any
            // trailing newline is layout noise. `}` is excluded: it closes a
            // block where the preceding newline is a real statement separator.
            if (nextReal is { Kind: TokenKind.RightParen or TokenKind.RightBracket }) {
                continue;
            }
            result.Add(t);
        }
        return result;
    }

    private static Token? FindPreviousReal(List<Token> raw, int index) {
        for (int j = index - 1; j >= 0; j--) {
            if (raw[j].Kind != TokenKind.Newline) return raw[j];
        }
        return null;
    }

    private static Token? FindNextReal(List<Token> raw, int index) {
        for (int j = index + 1; j < raw.Count; j++) {
            if (raw[j].Kind != TokenKind.Newline) return raw[j];
        }
        return null;
    }

    private static bool IsContinuationEligible(TokenKind kind) =>
        kind switch {
            // Binary arithmetic, comparison, logical.
            TokenKind.Plus or TokenKind.Minus or TokenKind.Star or TokenKind.Slash
                or TokenKind.Percent => true,
            TokenKind.EqualEqual or TokenKind.BangEqual
                or TokenKind.Less or TokenKind.Greater
                or TokenKind.LessEqual or TokenKind.GreaterEqual => true,
            TokenKind.AmpAmp or TokenKind.PipePipe or TokenKind.QuestionQuestion => true,
            // Assignments.
            TokenKind.Assign or TokenKind.ColonAssign
                or TokenKind.PlusAssign or TokenKind.MinusAssign
                or TokenKind.StarAssign or TokenKind.SlashAssign
                or TokenKind.PercentAssign => true,
            // Comma — list/argument continuation.
            TokenKind.Comma => true,
            // Opening brackets — content continues on the next line.
            // NOTE: LeftBrace is NOT included — `{` opens a block where the
            // next newline is the statement separator.
            TokenKind.LeftParen or TokenKind.LeftBracket
                or TokenKind.HashBrace => true,
            // Member access / null-conditional access — trailing form.
            TokenKind.Dot or TokenKind.QuestionDot => true,
            // Lambda arrow.
            TokenKind.Arrow => true,
            // Ternary opener.
            TokenKind.Question => true,
            _ => false,
        };

    // ---------------------------------------------------------------------
    // Frames
    // ---------------------------------------------------------------------

    private readonly record struct InterpFrame(int OuterDepth);
}
