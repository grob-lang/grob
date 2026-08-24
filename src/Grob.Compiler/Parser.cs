using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

/// <summary>
/// The Grob v1 recursive-descent parser. Builds a <see cref="CompilationUnit"/>
/// from a lexer token stream while reporting every syntactic problem the user
/// has — never giving up after the first error. The recovery model is the one
/// in grob-language-fundamentals §29 (single shared <c>Synchronise</c> routine,
/// first-class <see cref="ErrorExpr"/> / <see cref="ErrorStmt"/> /
/// <see cref="ErrorDecl"/> placeholders, no diagnostic cap).
/// </summary>
public sealed class Parser {
    private static readonly ErrorDescriptor _e2001 = ErrorCatalog.E2001;

    private readonly IReadOnlyList<Token> _tokens;
    private readonly DiagnosticBag _diagnostics;
    private int _pos;

    // When false, the LeftBrace case in ParsePostfix is suppressed so that
    // 'identifier {' is never parsed as struct construction.  Set to false
    // in positions where '{' is unambiguously a block starter (for-in iterable,
    // select case patterns) so those blocks are not consumed by the postfix rule.
    private bool _allowStructLiteral = true;

    /// <summary>Parse a token stream into a <see cref="CompilationUnit"/>.</summary>
    /// <param name="tokens">The lexer output. Must end with an <see cref="TokenKind.Eof"/> token.</param>
    /// <param name="diagnostics">The bag every parser diagnostic is appended to.</param>
    public static CompilationUnit Parse(IReadOnlyList<Token> tokens, DiagnosticBag diagnostics) {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (tokens.Count == 0) {
            throw new ArgumentException("Token stream must include at least an EOF token.", nameof(tokens));
        }
        if (tokens[^1].Kind != TokenKind.Eof) {
            throw new ArgumentException("Token stream must terminate with an EOF token.", nameof(tokens));
        }
        return new Parser(tokens, diagnostics).ParseCompilationUnit();
    }

    private Parser(IReadOnlyList<Token> tokens, DiagnosticBag diagnostics) {
        _tokens = tokens;
        _diagnostics = diagnostics;
    }

    // -----------------------------------------------------------------------
    // Cursor primitives
    // -----------------------------------------------------------------------

    private Token Current => _tokens[_pos];

    private Token PeekAt(int offset) {
        int i = _pos + offset;
        return i < _tokens.Count ? _tokens[i] : _tokens[^1];
    }

    private bool IsAtEnd => Current.Kind == TokenKind.Eof;

    private Token Advance() {
        Token t = Current;
        if (!IsAtEnd) {
            _pos++;
        }
        return t;
    }

    private bool Check(TokenKind k) => Current.Kind == k;

    private bool Match(TokenKind k) {
        if (Check(k)) {
            Advance();
            return true;
        }
        return false;
    }

    private void SkipNewlines() {
        while (Check(TokenKind.Newline)) {
            Advance();
        }
    }

    private Token Expect(TokenKind k, ErrorDescriptor descriptor, string message) {
        if (Check(k)) {
            return Advance();
        }
        throw Fail(descriptor, message);
    }

    // -----------------------------------------------------------------------
    // Failure / diagnostics
    // -----------------------------------------------------------------------

    private ParseFailedException Fail(ErrorDescriptor descriptor, string message) {
        SourceRange range = new(Current.Location, Current.Location);
        Diagnostic d = Diagnostic.Of(descriptor, range, message);
        _diagnostics.Add(d);
        return new ParseFailedException(d);
    }

    /// <summary>
    /// Like <see cref="Fail"/> but pins the diagnostic at an explicit
    /// <paramref name="loc"/> rather than at <see cref="Current"/>.
    /// Used when the real failure site (e.g. a dangling binary operator) is
    /// already behind the cursor because line-continuation suppressed the
    /// newline that would have separated it from the next token.
    /// </summary>
    private ParseFailedException FailAt(SourceLocation loc, ErrorDescriptor descriptor, string message) {
        SourceRange range = new(loc, loc);
        Diagnostic d = Diagnostic.Of(descriptor, range, message);
        _diagnostics.Add(d);
        return new ParseFailedException(d);
    }

    private static SourceRange RangeBetween(SourceLocation start, SourceLocation end) {
        bool endBeforeStart =
            end.File != start.File ||
            end.Line < start.Line ||
            (end.Line == start.Line && end.Column < start.Column);
        return new SourceRange(start, endBeforeStart ? start : end);
    }

    private SourceRange RangeFrom(SourceLocation start) => RangeBetween(start, Current.Location);

    // -----------------------------------------------------------------------
    // Synchronise (§29, amended by D-415). Anchors:
    //   * Newline at BracketDepth == 0 and no locally-opened brace.
    //   * }  closing an enclosing block (not one we opened inside the skip).
    //   * Top-level declaration keywords (fn/type/param/import/const/readonly).
    //   * '@' — the token that opens a top-level declaration's decorator stack
    //     (D-415). Not bracket-depth-gated, matching '}' — a decorator can only
    //     ever legally appear immediately above a param declaration, so landing
    //     recovery there is never the wrong place to stop, unlike a bare newline.
    //     Without this, a still-open bracket from an earlier failure (an
    //     unterminated param default) leaves the newline anchor permanently
    //     disabled, and Synchronise would otherwise skip straight over an intact
    //     decorator stack to the param keyword below it — silently discarding it
    //     before the '@' dispatch arm ever sees it.
    //   * EOF (unconditional terminator).
    // The cursor stops AT the anchor; the anchor is not consumed.
    // -----------------------------------------------------------------------

    private void Synchronise() {
        int localOpenBraces = 0;
        while (!IsAtEnd) {
            TokenKind k = Current.Kind;
            if (localOpenBraces == 0 && IsSyncAnchor(k)) return;
            if (k == TokenKind.LeftBrace) {
                localOpenBraces++;
            } else if (k == TokenKind.RightBrace) {
                localOpenBraces--;
            }
            Advance();
        }
    }

    private bool IsSyncAnchor(TokenKind k) {
        if (k == TokenKind.Newline && Current.BracketDepth == 0) return true;
        if (k == TokenKind.RightBrace) return true;
        if (k == TokenKind.At) return true;
        return IsTopLevelKeyword(k);
    }

    private static bool IsTopLevelKeyword(TokenKind k) =>
        k is TokenKind.Fn or TokenKind.Type or TokenKind.Param
          or TokenKind.Import or TokenKind.Const or TokenKind.Readonly;

    /// <summary>
    /// Returns <see langword="true"/> for binary-operator token kinds that require
    /// a right-hand operand. Used by <see cref="ParsePrimary"/> to detect the case
    /// where line-continuation suppressed the newline after a dangling operator,
    /// so the diagnostic can point at the operator rather than at the next token.
    /// </summary>
    private static bool IsBinaryOperatorForContext(TokenKind k) =>
        k is TokenKind.Plus or TokenKind.Minus or TokenKind.Star or TokenKind.Slash
          or TokenKind.Percent
          or TokenKind.EqualEqual or TokenKind.BangEqual
          or TokenKind.Less or TokenKind.Greater
          or TokenKind.LessEqual or TokenKind.GreaterEqual
          or TokenKind.AmpAmp or TokenKind.PipePipe or TokenKind.QuestionQuestion;

    // -----------------------------------------------------------------------
    // Recovery wrappers — one per AST sort. Every entry point that risks
    // failure passes through these. Synchronise is called only here.
    // -----------------------------------------------------------------------

    private AstNode ParseTopLevelItemOrError() {
        SourceLocation start = Current.Location;
        int startPos = _pos;
        try {
            return ParseTopLevelItem();
        } catch (ParseFailedException ex) {
            if (_pos == startPos && !IsAtEnd) {
                Advance();
            }
            Synchronise();
            // §29.2: error-node range is exclusive of the anchor token — use the
            // last consumed token's location as End, not Current (the anchor).
            SourceLocation end = _pos > 0 ? _tokens[_pos - 1].Location : start;
            return new ErrorDecl(RangeBetween(start, end), ex.Diagnostic);
        }
    }

    private Statement ParseStatementOrError() {
        SourceLocation start = Current.Location;
        int startPos = _pos;
        try {
            return ParseStatement();
        } catch (ParseFailedException ex) {
            if (_pos == startPos && !IsAtEnd) {
                Advance();
            }
            Synchronise();
            // §29.2: error-node range is exclusive of the anchor token.
            SourceLocation end = _pos > 0 ? _tokens[_pos - 1].Location : start;
            return new ErrorStmt(RangeBetween(start, end), ex.Diagnostic);
        }
    }

    private Expression ExpressionOrError() {
        SourceLocation start = Current.Location;
        int startPos = _pos;
        try {
            return ParseExpression();
        } catch (ParseFailedException ex) {
            if (_pos == startPos && !IsAtEnd) {
                Advance();
            }
            Synchronise();
            // §29.2: error-node range is exclusive of the anchor token.
            SourceLocation end = _pos > 0 ? _tokens[_pos - 1].Location : start;
            return new ErrorExpr(RangeBetween(start, end), ex.Diagnostic);
        }
    }

    // -----------------------------------------------------------------------
    // Compilation unit
    // -----------------------------------------------------------------------

    private CompilationUnit ParseCompilationUnit() {
        SourceLocation start = _tokens[0].Location;
        List<AstNode> items = [];
        SkipNewlines();
        while (!IsAtEnd) {
            items.Add(ParseTopLevelItemOrError());
            SkipNewlines();
        }
        SourceLocation end = _tokens[^1].Location;
        return new CompilationUnit(new SourceRange(start, end), items);
    }

    private AstNode ParseTopLevelItem() => Current.Kind switch {
        TokenKind.Fn => ParseFnDecl(),
        TokenKind.Type => ParseTypeDecl(),
        TokenKind.Param => ParseParamDecl(),
        // A decorator stack is part of the param-declaration production it
        // precedes (§19: "{ decorator newline } 'param' ..."), so it is parsed
        // by the same method rather than a separate lookahead that re-derives
        // the same fact (D-415 gate item 5).
        TokenKind.At => ParseParamDecl(),
        TokenKind.Import => ParseImportDecl(),
        TokenKind.Const => ParseConstDecl(false),
        TokenKind.Readonly => ParseReadonlyDecl(),
        _ => ParseStatement(),
    };

    // -----------------------------------------------------------------------
    // Declarations
    // -----------------------------------------------------------------------

    private ImportDecl ParseImportDecl() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.Import, _e2001, "expected 'import'");
        Token name = Expect(TokenKind.Identifier, _e2001, "expected module name after 'import'");
        string modulePath = name.Lexeme;
        // Dotted path: foo.bar.baz
        while (Match(TokenKind.Dot)) {
            Token seg = Expect(TokenKind.Identifier, _e2001, "expected identifier after '.' in module path");
            modulePath = modulePath + "." + seg.Lexeme;
        }
        string? alias = null;
        if (Match(TokenKind.As)) {
            Token a = Expect(TokenKind.Identifier, _e2001, "expected alias name after 'as'");
            alias = a.Lexeme;
        }
        return new ImportDecl(RangeFrom(start), modulePath, alias);
    }

    private ConstDecl ParseConstDecl(bool alreadyConsumedKeyword) {
        SourceLocation start = alreadyConsumedKeyword ? PeekAt(-1).Location : Current.Location;
        if (!alreadyConsumedKeyword) {
            Expect(TokenKind.Const, _e2001, "expected 'const'");
        }
        Token name = Expect(TokenKind.Identifier, _e2001, "expected name after 'const'");
        TypeRef? annotatedType = null;
        if (Match(TokenKind.Colon)) {
            annotatedType = ParseTypeRef();
        }
        Expect(TokenKind.ColonAssign, _e2001, "expected ':=' in const declaration");
        Expression value = ParseExpression();
        return new ConstDecl(RangeFrom(start), name.Lexeme, annotatedType, value);
    }

    private ReadonlyDecl ParseReadonlyDecl() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.Readonly, _e2001, "expected 'readonly'");
        Token name = Expect(TokenKind.Identifier, _e2001, "expected name after 'readonly'");
        TypeRef? annotatedType = null;
        if (Match(TokenKind.Colon)) {
            annotatedType = ParseTypeRef();
        }
        Expect(TokenKind.ColonAssign, _e2001, "expected ':=' in readonly declaration");
        Expression value = ParseExpression();
        return new ReadonlyDecl(RangeFrom(start), name.Lexeme, annotatedType, value);
    }

    private FnDecl ParseFnDecl() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.Fn, _e2001, "expected 'fn'");
        Token name = Expect(TokenKind.Identifier, _e2001, "expected function name after 'fn'");
        Expect(TokenKind.LeftParen, _e2001, "expected '(' in function declaration");
        List<Parameter> parameters = ParseParameterList(TokenKind.RightParen);
        Expect(TokenKind.RightParen, _e2001, "expected ')' to close parameter list");
        Expect(TokenKind.Colon, _e2001, "expected ':' followed by return type");
        TypeRef returnType = ParseTypeRef();
        BlockStmt body = ParseBlock();
        return new FnDecl(RangeFrom(start), name.Lexeme, parameters, returnType, body);
    }

    private TypeDecl ParseTypeDecl() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.Type, _e2001, "expected 'type'");
        Token name = Expect(TokenKind.Identifier, _e2001, "expected type name after 'type'");
        Expect(TokenKind.LeftBrace, _e2001, "expected '{' to open type body");
        SkipNewlines();
        List<TypeField> fields = [];
        while (!Check(TokenKind.RightBrace) && !IsAtEnd) {
            // Mirrors ParseBlock's own top-level-keyword anchor (§29): without this,
            // a field left unconsumed at a keyword by SkipToNextLiteralElementBoundary
            // (D-406) would be force-advanced over on the loop's next attempt, eating
            // the keyword the resync deliberately stopped at.
            if (IsTopLevelKeyword(Current.Kind)) break;
            TypeField? field = ParseTypeFieldOrError();
            if (field is not null) fields.Add(field);
            SkipNewlines();
        }
        Expect(TokenKind.RightBrace, _e2001, "expected '}' to close type body");
        return new TypeDecl(RangeFrom(start), name.Lexeme, fields);
    }

    private TypeField ParseTypeField() {
        SourceLocation start = Current.Location;
        Token name = Expect(TokenKind.Identifier, _e2001, "expected field name");
        Expect(TokenKind.Colon, _e2001, "expected ':' after field name");
        TypeRef type = ParseTypeRef();
        Expression? defaultValue = null;
        if (Match(TokenKind.Assign)) {
            defaultValue = ParseExpression();
        }
        return new TypeField(RangeFrom(start), name.Lexeme, type, defaultValue);
    }

    /// <summary>
    /// Local recovery wrapper (D-406) around <see cref="ParseTypeField"/> — a
    /// <c>type</c> body is a newline-separated, brace-delimited element list, the
    /// declaration-context sibling of D-405's comma-separated literal interiors.
    /// Resynchronises via <see cref="SkipToNextLiteralElementBoundary"/> in its
    /// newline-boundary mode, which also anchors on a top-level declaration
    /// keyword — see that method's doc comment. Returns <see langword="null"/> for
    /// a malformed field — the field is omitted from the type, not replaced by a
    /// placeholder node.
    /// </summary>
    private TypeField? ParseTypeFieldOrError() {
        int startPos = _pos;
        try {
            return ParseTypeField();
        } catch (ParseFailedException) {
            if (_pos == startPos && !IsAtEnd) {
                Advance();
            }
            SkipToNextLiteralElementBoundary(startPos, TokenKind.Newline);
            return null;
        }
    }

    /// <summary>
    /// A single braceless <c>param</c> declaration (D-410): <c>{ decorator
    /// newline } "param" identifier ":" type [ "=" expression ] newline</c>. One
    /// <c>param</c> keyword per parameter — there is no enclosing block, so
    /// (unlike the retired <c>ParseParamBlockDecl</c>) this needs no local element
    /// loop and no local recovery wrapper of its own: each declaration is an
    /// ordinary top-level item, already covered by <see cref="ParseTopLevelItemOrError"/>
    /// and the keyword/'@'-anchored <see cref="Synchronise"/> (D-415) exactly as
    /// <c>const</c>/<c>readonly</c>/<c>import</c> are. Consecutive <c>param</c>
    /// declarations form a parameter group by contiguity purely because the
    /// top-level loop (<see cref="ParseCompilationUnit"/>) parses each one as its
    /// own item — no dedicated grouping code exists or is needed.
    /// <para>
    /// Reachable via two dispatch arms in <see cref="ParseTopLevelItem"/>: directly
    /// at <see cref="TokenKind.Param"/> (no decorators), and at
    /// <see cref="TokenKind.At"/> (a decorator stack precedes it). Both call this
    /// method, which itself starts by skipping any decorator stack — the stack is
    /// part of this production (§19), not a separate construct.
    /// </para>
    /// <para>
    /// Every failure inside this production's own grammar (not inside
    /// <see cref="ParseTypeRef"/> or <see cref="ParseExpression"/>, which stay
    /// generic E2001) is reported as <see cref="ErrorCatalog.E4201"/> — the
    /// mandatory-type-annotation check, the decorator-must-be-followed-by-'param'
    /// check, and the <c>:=</c>-instead-of-<c>=</c> default check all give E4201
    /// its throw sites (D-407 pattern).
    /// </para>
    /// </summary>
    private ParamDecl ParseParamDecl() {
        SkipParameterDecorators();
        SourceLocation start = Current.Location;
        Expect(TokenKind.Param, ErrorCatalog.E4201, "expected 'param' after decorator");
        Token name = Expect(TokenKind.Identifier, ErrorCatalog.E4201, "expected parameter name after 'param'");
        Expect(TokenKind.Colon, ErrorCatalog.E4201,
            "expected ':' after parameter name — the type annotation is mandatory");
        TypeRef type = ParseTypeRef();
        Expression? defaultValue = null;
        if (Check(TokenKind.ColonAssign)) {
            throw Fail(ErrorCatalog.E4201, "expected '=' for a parameter default, not ':='");
        }
        if (Match(TokenKind.Assign)) {
            defaultValue = ParseExpression();
        }
        return new ParamDecl(RangeFrom(start), name.Lexeme, type, defaultValue);
    }

    private Parameter ParseDeclaredParameter() {
        // Decorators (@allowed, @minLength, …) are tokens at this point; we
        // consume them as opaque sequences in v1 — the type checker handles
        // their semantic content later.
        SourceLocation start = Current.Location;
        SkipParameterDecorators();
        Token name = Expect(TokenKind.Identifier, _e2001, "expected parameter name");
        Expect(TokenKind.Colon, _e2001, "expected ':' after parameter name");
        TypeRef type = ParseTypeRef();
        Expression? defaultValue = null;
        if (Match(TokenKind.Assign)) {
            defaultValue = ParseExpression();
        }
        return new Parameter(RangeFrom(start), name.Lexeme, type, defaultValue);
    }

    private void SkipParameterDecorators() {
        while (Match(TokenKind.At)) {
            Expect(TokenKind.Identifier, _e2001, "expected decorator name after '@'");
            if (Match(TokenKind.LeftParen)) {
                SkipBalancedDecoratorArgs();
            }
            SkipNewlines();
        }
    }

    private void SkipBalancedDecoratorArgs() {
        int depth = 1;
        while (depth > 0 && !IsAtEnd) {
            TokenKind k = Current.Kind;
            if (k == TokenKind.LeftParen) depth++;
            else if (k == TokenKind.RightParen) depth--;
            if (depth > 0) Advance();
        }
        Expect(TokenKind.RightParen, _e2001, "expected ')' to close decorator arguments");
    }

    private List<Parameter> ParseParameterList(TokenKind terminator) {
        List<Parameter> list = [];
        SkipNewlines();
        if (Check(terminator)) return list;
        list.Add(ParseDeclaredParameter());
        while (Match(TokenKind.Comma)) {
            SkipNewlines();
            list.Add(ParseDeclaredParameter());
        }
        SkipNewlines();
        return list;
    }

    // -----------------------------------------------------------------------
    // Type references (D-327: primary-plus-suffix grammar)
    // -----------------------------------------------------------------------

    private TypeRef ParseTypeRef() {
        SourceLocation start = Current.Location;
        TypeRef current = ParseTypePrimary(start);

        // Suffix loop: consume [] and ? left to right (D-327).
        // [] wraps the current type as its element type; ? marks the current type nullable.
        while (Check(TokenKind.LeftBracket) || Check(TokenKind.Question)) {
            if (Match(TokenKind.LeftBracket)) {
                if (Check(TokenKind.IntLiteral) && PeekAt(1).Kind == TokenKind.RightBracket) {
                    throw Fail(_e2001, "fixed-size array types are not supported — use 'T[]'");
                }
                Expect(TokenKind.RightBracket, _e2001, "expected ']' to close array type suffix");
                current = new ArrayTypeRef(RangeFrom(start), current, IsNullable: false);
            } else {
                Advance(); // consume ?
                current = current with { IsNullable = true, Range = RangeFrom(start) };
            }
        }

        return current;
    }

    private TypeRef ParseTypePrimary(SourceLocation start) {
        // Parenthesised type — supplies grouping so that ? and [] suffixes bind to
        // the whole group rather than the return type: (fn(): T)? and (fn(): T)[].
        // The grouped type's range starts at the opening '(' (RangeFrom(start)) so a
        // diagnostic on the group points at the whole group. D-327 generalises
        // D-326's dedicated '(' TypeRef ')' '?' production into this grouping primary
        // plus the shared suffix loop above.
        if (Match(TokenKind.LeftParen)) {
            TypeRef inner = ParseTypeRef();
            Expect(TokenKind.RightParen, _e2001, "expected ')' to close parenthesised type");
            return inner with { Range = RangeFrom(start) };
        }

        // Function type — fn(T1, T2): R (D-326). Suffixes bind to the return type
        // (parsed recursively by the suffix loop inside ParseTypeRef), not to the fn
        // itself — that requires grouping parens: (fn(): T)? or (fn(): T)[].
        if (Match(TokenKind.Fn)) {
            Expect(TokenKind.LeftParen, _e2001, "expected '(' after 'fn' in function type");
            List<TypeRef> paramTypes = [];
            if (!Check(TokenKind.RightParen) && !IsAtEnd) {
                paramTypes.Add(ParseTypeRef());
                while (Match(TokenKind.Comma)) paramTypes.Add(ParseTypeRef());
            }
            Expect(TokenKind.RightParen, _e2001, "expected ')' to close function type parameters");
            Expect(TokenKind.Colon, _e2001, "expected ':' after function type parameters");
            TypeRef returnType = ParseTypeRef();
            return new FunctionTypeRef(RangeFrom(start), paramTypes, returnType, IsNullable: false);
        }

        // Identifier-named type with optional generic arguments.
        Token name = Expect(TokenKind.Identifier, _e2001, "expected type name");
        List<TypeRef> args = [];
        if (Match(TokenKind.Less)) {
            args = ParseTypeArgumentList();
        }
        return new TypeRef(RangeFrom(start), name.Lexeme, args, IsNullable: false);
    }

    /// <summary>
    /// Parses a generic type-argument list's contents — one <c>TypeRef</c>, then any
    /// number of comma-separated further ones — up to and including the closing
    /// <c>'&gt;'</c>. The opening <c>'&lt;'</c> must already be consumed by the caller
    /// (D-376): shared between <see cref="ParseTypePrimary"/>'s ordinary generic-argument
    /// position and the map-literal path (<see cref="ParseMapLiteral"/>), which needs the
    /// identical production for <c>map&lt;K, V&gt;</c>'s header before its own <c>{ }</c>
    /// body.
    /// </summary>
    private List<TypeRef> ParseTypeArgumentList() {
        List<TypeRef> args = [ParseTypeRef()];
        while (Match(TokenKind.Comma)) {
            args.Add(ParseTypeRef());
        }
        Expect(TokenKind.Greater, _e2001, "expected '>' to close generic arguments");
        return args;
    }

    // -----------------------------------------------------------------------
    // Statements
    // -----------------------------------------------------------------------

    private BlockStmt ParseBlock() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.LeftBrace, _e2001, "expected '{'");
        SkipNewlines();
        List<Statement> stmts = [];
        while (!Check(TokenKind.RightBrace) && !IsAtEnd) {
            // §29 anchor: a top-level keyword at depth 0 of the block ends the
            // block. Expect(RightBrace) then reports the missing brace and the
            // surrounding declaration is wrapped as an ErrorDecl by recovery.
            if (IsTopLevelKeyword(Current.Kind)) break;
            stmts.Add(ParseStatementOrError());
            SkipNewlines();
        }
        Expect(TokenKind.RightBrace, _e2001, "expected '}'");
        return new BlockStmt(RangeFrom(start), stmts);
    }

    private Statement ParseStatement() {
        // 'select' is a reserved identifier, not a keyword (D-320), so it arrives as
        // an Identifier. At statement head it has exactly one statement meaning —
        // the select statement — recognised by a following '('. A leading 'select'
        // used as a binding name ('select :=', 'select: T :=') falls through to the
        // declaration path so the type checker reports E1103. Any other leading
        // 'select' is a malformed select statement.
        if (Current.Kind == TokenKind.Identifier && Current.Lexeme == "select") {
            return ParseLeadingSelectStatement();
        }
        switch (Current.Kind) {
            case TokenKind.LeftBrace: return ParseBlock();
            case TokenKind.If: return ParseIf();
            case TokenKind.While: return ParseWhile();
            case TokenKind.For: return ParseForIn();
            case TokenKind.Return: return ParseReturn();
            case TokenKind.Break: {
                    SourceLocation s = Current.Location;
                    Advance();
                    return new BreakStmt(RangeFrom(s));
                }
            case TokenKind.Continue: {
                    SourceLocation s = Current.Location;
                    Advance();
                    return new ContinueStmt(RangeFrom(s));
                }
            case TokenKind.Try: return ParseTry();
            case TokenKind.Throw: return ParseThrow();
            case TokenKind.Const: return ParseConstDeclAsStatement();
            default: return ParseExpressionOrAssignmentStatement();
        }
    }

    private Statement ParseConstDeclAsStatement() {
        // Top-level wraps `const` as a Declaration. Block-level const lands in
        // Sprint 2 (\u00a724); for now we consume the declaration so recovery picks
        // up past it, then report it as a parse error.
        _ = ParseConstDecl(false);
        throw Fail(_e2001, "'const' is only allowed at the top level in Sprint 1");
    }

    private IfStmt ParseIf() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.If, _e2001, "expected 'if'");
        Expect(TokenKind.LeftParen, _e2001, "expected '(' before if condition");
        Expression cond = ParseExpression();
        Expect(TokenKind.RightParen, _e2001, "expected ')' after if condition");
        BlockStmt then = ParseBlock();
        Statement? elseBranch = null;
        // `else` may follow on the next line — skip newlines after the
        // closing brace of the then-branch.
        int saved = _pos;
        SkipNewlines();
        if (Match(TokenKind.Else)) {
            elseBranch = Check(TokenKind.If) ? ParseIf() : ParseBlock();
        } else {
            _pos = saved;
        }
        return new IfStmt(RangeFrom(start), cond, then, elseBranch);
    }

    private WhileStmt ParseWhile() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.While, _e2001, "expected 'while'");
        Expect(TokenKind.LeftParen, _e2001, "expected '(' before while condition");
        Expression cond = ParseExpression();
        Expect(TokenKind.RightParen, _e2001, "expected ')' after while condition");
        BlockStmt body = ParseBlock();
        return new WhileStmt(RangeFrom(start), cond, body);
    }

    private ForInStmt ParseForIn() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.For, _e2001, "expected 'for'");
        Token first = Expect(TokenKind.Identifier, _e2001, "expected loop variable after 'for'");
        List<string> vars = [first.Lexeme];
        if (Match(TokenKind.Comma)) {
            Token second = Expect(TokenKind.Identifier, _e2001, "expected second loop variable after ','");
            vars.Add(second.Lexeme);
        }
        Expect(TokenKind.In, _e2001, "expected 'in' in for-loop header");
        Expression iterable = ParseIterable();
        BlockStmt body = ParseBlock();
        return new ForInStmt(RangeFrom(start), vars, iterable, body);
    }

    private Expression ParseIterable() {
        SourceLocation start = Current.Location;
        // Disable struct construction so that 'for x in xs { }' is not parsed as
        // 'xs { }' (struct-construction expression); the '{' must open the loop body.
        bool prevAllow = _allowStructLiteral;
        _allowStructLiteral = false;
        Expression iter;
        try {
            iter = ParseExpression();
        } finally {
            _allowStructLiteral = prevAllow;
        }
        if (Match(TokenKind.DotDot)) {
            Expression end = ParseExpression();
            Expression? step = null;
            if (Match(TokenKind.Step)) {
                step = ParseExpression();
            }
            return new NumericRangeExpr(RangeFrom(start), iter, end, step);
        }
        return iter;
    }

    private ReturnStmt ParseReturn() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.Return, _e2001, "expected 'return'");
        Expression? value = null;
        if (!Check(TokenKind.Newline) && !Check(TokenKind.RightBrace) && !IsAtEnd) {
            value = ExpressionOrError();
        }
        return new ReturnStmt(RangeFrom(start), value);
    }

    private ThrowStmt ParseThrow() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.Throw, _e2001, "expected 'throw'");
        // Unlike 'return', the operand is mandatory (D-274) — no bare-throw form.
        Expression value = ExpressionOrError();
        return new ThrowStmt(RangeFrom(start), value);
    }

    private TryStmt ParseTry() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.Try, _e2001, "expected 'try'");
        BlockStmt body = ParseBlock();
        List<CatchClause> catches = [];
        BlockStmt? @finally = null;
        // catch / finally may follow on the next line.
        while (true) {
            int saved = _pos;
            SkipNewlines();
            if (Match(TokenKind.Catch)) {
                catches.Add(FinishCatchClause(PeekAt(-1).Location));
                continue;
            }
            if (Match(TokenKind.Finally)) {
                @finally = ParseBlock();
                // 'finally' must be the last clause (D-275). A further catch or
                // finally after it is E2206 — the AST has nowhere to put it, so
                // this is a parse-time diagnostic, not a type-checker one.
                int savedAfterFinally = _pos;
                SkipNewlines();
                if (Check(TokenKind.Catch) || Check(TokenKind.Finally)) {
                    throw Fail(ErrorCatalog.E2206, "'finally' must be the last clause of a 'try'.");
                }
                _pos = savedAfterFinally;
                break;
            }
            _pos = saved;
            break;
        }
        return new TryStmt(RangeFrom(start), body, catches, @finally);
    }

    private CatchClause FinishCatchClause(SourceLocation start) {
        TypeRef? exceptionType = null;
        string? exceptionVar = null;
        if (Match(TokenKind.LeftParen)) {
            if (Check(TokenKind.Identifier) && PeekAt(1).Kind == TokenKind.Colon) {
                Token name = Advance();
                exceptionVar = name.Lexeme;
                Advance(); // colon
                exceptionType = ParseTypeRef();
                Expect(TokenKind.RightParen, _e2001, "expected ')' to close catch clause header");
            } else {
                // 'catch (e)' (identifier, no type) and 'catch ()' (empty) are
                // both a syntax error (D-274) — the parenthesised form always
                // requires a typed binding 'catch (<name>: <Type>)'.
                throw Fail(_e2001,
                    "'catch (...)' requires a typed binding 'catch (<name>: <Type>)'; "
                  + "'catch (e)' with no type is not valid. Use 'catch <name> { }' for "
                  + "the catch-all form.");
            }
        } else {
            // No-parens catch-all form: 'catch <name> { }' — the identifier is
            // mandatory (D-274); it binds the caught value, typed as GrobError.
            Token name = Expect(TokenKind.Identifier, _e2001,
                "expected an identifier after 'catch' (the catch-all form is 'catch <name> { }')");
            exceptionVar = name.Lexeme;
        }
        BlockStmt body = ParseBlock();
        return new CatchClause(RangeFrom(start), exceptionType, exceptionVar, body);
    }

    /// <summary>
    /// Dispatches a leading 'select' identifier at statement head (D-320). The
    /// caller has confirmed <see cref="Current"/> is the identifier 'select'.
    /// </summary>
    private Statement ParseLeadingSelectStatement() {
        TokenKind next = PeekAt(1).Kind;
        if (next == TokenKind.LeftParen) return ParseSelect();
        // A binding-declaration form ('select :=' or 'select: T :=') is a name use,
        // not a statement: parse it normally so the type checker reports E1103.
        if (next == TokenKind.ColonAssign || next == TokenKind.Colon) {
            return ParseExpressionOrAssignmentStatement();
        }
        throw Fail(_e2001,
            "'select' must be followed by '(' — the select statement form is "
          + "'select (subject) { case ... }'");
    }

    private SelectStmt ParseSelect() {
        SourceLocation start = Current.Location;
        // 'select' lexes as an identifier (D-320); consume it as the statement head.
        Advance();
        Expect(TokenKind.LeftParen, _e2001, "expected '(' before select subject");
        Expression subject = ParseExpression();
        Expect(TokenKind.RightParen, _e2001, "expected ')' after select subject");
        Expect(TokenKind.LeftBrace, _e2001, "expected '{' to open select body");
        SkipNewlines();
        List<CaseClause> cases = [];
        BlockStmt? defaultBlock = null;
        while (!Check(TokenKind.RightBrace) && !IsAtEnd) {
            if (Match(TokenKind.Default)) {
                defaultBlock = ParseBlock();
                SkipNewlines();
                continue;
            }
            SourceLocation cs = Current.Location;
            Expect(TokenKind.Case, _e2001, "expected 'case' or 'default'");
            // Disable struct construction in case patterns so that 'case y { }' is
            // not parsed as pattern 'y { }' (struct construction) consuming the body '{}'.
            bool prevAllow = _allowStructLiteral;
            _allowStructLiteral = false;
            List<Expression> patterns;
            try {
                patterns = [ParseExpression()];
                while (Match(TokenKind.Comma)) {
                    patterns.Add(ParseExpression());
                }
            } finally {
                _allowStructLiteral = prevAllow;
            }
            BlockStmt body = ParseBlock();
            cases.Add(new CaseClause(RangeFrom(cs), patterns, body));
            SkipNewlines();
        }
        Expect(TokenKind.RightBrace, _e2001, "expected '}' to close select body");
        return new SelectStmt(RangeFrom(start), subject, cases, defaultBlock);
    }

    private Statement ParseExpressionOrAssignmentStatement() {
        SourceLocation start = Current.Location;
        // Variable declaration `name := expr` or `name: Type := expr`.
        if (Check(TokenKind.Identifier)) {
            TokenKind nextKind = PeekAt(1).Kind;
            if (nextKind == TokenKind.ColonAssign) {
                Token name = Advance();
                Advance(); // :=
                Expression init = ParseExpression();
                return new VarDeclStmt(RangeFrom(start), name.Lexeme, null, init);
            }
            // Distinguish `name: Type := expr` (decl) from `name: ...` in some
            // other context. Sprint 1 grammar: `:` after a top-level identifier
            // only ever begins a typed var declaration.
            if (nextKind == TokenKind.Colon && IsTypedVarDecl()) {
                Token name = Advance();
                Advance(); // :
                TypeRef type = ParseTypeRef();
                Expect(TokenKind.ColonAssign, _e2001, "expected ':=' after declared type");
                Expression init = ParseExpression();
                return new VarDeclStmt(RangeFrom(start), name.Lexeme, type, init);
            }
        }

        Expression target = ParseExpression();

        switch (Current.Kind) {
            case TokenKind.Assign: {
                    Advance();
                    Expression value = ParseExpression();
                    return new AssignmentStmt(RangeFrom(start), target, value);
                }
            case TokenKind.PlusAssign: return FinishCompound(start, target, CompoundAssignmentOperator.PlusAssign);
            case TokenKind.MinusAssign: return FinishCompound(start, target, CompoundAssignmentOperator.MinusAssign);
            case TokenKind.StarAssign: return FinishCompound(start, target, CompoundAssignmentOperator.StarAssign);
            case TokenKind.SlashAssign: return FinishCompound(start, target, CompoundAssignmentOperator.SlashAssign);
            case TokenKind.PercentAssign: return FinishCompound(start, target, CompoundAssignmentOperator.PercentAssign);
            case TokenKind.PlusPlus: {
                    Advance();
                    return new IncrementStmt(RangeFrom(start), target, IncrementKind.Increment);
                }
            case TokenKind.MinusMinus: {
                    Advance();
                    return new IncrementStmt(RangeFrom(start), target, IncrementKind.Decrement);
                }
        }
        return new ExpressionStmt(RangeFrom(start), target);
    }

    private CompoundAssignmentStmt FinishCompound(SourceLocation start, Expression target, CompoundAssignmentOperator op) {
        Advance();
        Expression value = ParseExpression();
        return new CompoundAssignmentStmt(RangeFrom(start), target, op, value);
    }

    private bool IsTypedVarDecl() {
        // Lookahead: identifier ':' identifier ('?'|'<'...|...)? ':='.
        // We only need to confirm the eventual `:=` exists at the same nesting.
        // For Sprint 1 this is a best-effort scan; on mismatch we treat the
        // construct as an expression.
        int i = _pos + 2; // past identifier, past ':'
        int parenDepth = 0;
        while (i < _tokens.Count) {
            TokenKind k = _tokens[i].Kind;
            if (parenDepth == 0) {
                switch (k) {
                    case TokenKind.ColonAssign:
                        return true;
                    case TokenKind.Newline:
                    case TokenKind.RightBrace:
                    case TokenKind.LeftBrace:
                    case TokenKind.Assign:
                    case TokenKind.Eof:
                        return false;
                }
            }
            if (k == TokenKind.Less) parenDepth++;
            else if (k == TokenKind.Greater) parenDepth--;
            i++;
        }
        return false;
    }

    // -----------------------------------------------------------------------
    // Expressions (Pratt; lowest precedence at top)
    // -----------------------------------------------------------------------

    private Expression ParseExpression() => ParseLambdaOrTernary();

    private Expression ParseLambdaOrTernary() {
        if (IsLambdaStart()) {
            return ParseLambda();
        }
        return ParseTernary();
    }

    private Expression ParseTernary() {
        Expression cond = ParseNilCoalesce();
        if (Match(TokenKind.Question)) {
            Expression thenE = ParseExpression();
            Expect(TokenKind.Colon, _e2001, "expected ':' in ternary expression");
            Expression elseE = ParseExpression();
            return new TernaryExpr(RangeBetween(cond.Range.Start, elseE.Range.End), cond, thenE, elseE);
        }
        return cond;
    }

    private Expression ParseNilCoalesce() {
        Expression left = ParseLogicalOr();
        while (Match(TokenKind.QuestionQuestion)) {
            Expression right = ParseLogicalOr();
            left = new BinaryExpr(RangeBetween(left.Range.Start, right.Range.End), BinaryOperator.NilCoalesce, left, right);
        }
        return left;
    }

    private Expression ParseLogicalOr() {
        Expression left = ParseLogicalAnd();
        while (Match(TokenKind.PipePipe)) {
            Expression right = ParseLogicalAnd();
            left = new BinaryExpr(RangeBetween(left.Range.Start, right.Range.End), BinaryOperator.Or, left, right);
        }
        return left;
    }

    private Expression ParseLogicalAnd() {
        Expression left = ParseEquality();
        while (Match(TokenKind.AmpAmp)) {
            Expression right = ParseEquality();
            left = new BinaryExpr(RangeBetween(left.Range.Start, right.Range.End), BinaryOperator.And, left, right);
        }
        return left;
    }

    private Expression ParseEquality() {
        Expression left = ParseComparison();
        while (true) {
            BinaryOperator? op = Current.Kind switch {
                TokenKind.EqualEqual => BinaryOperator.Equal,
                TokenKind.BangEqual => BinaryOperator.NotEqual,
                _ => null,
            };
            if (op is null) break;
            Advance();
            Expression right = ParseComparison();
            left = new BinaryExpr(RangeBetween(left.Range.Start, right.Range.End), op.Value, left, right);
        }
        return left;
    }

    private Expression ParseComparison() {
        Expression left = ParseAdditive();
        while (true) {
            BinaryOperator? op = Current.Kind switch {
                TokenKind.Less => BinaryOperator.Less,
                TokenKind.LessEqual => BinaryOperator.LessEqual,
                TokenKind.Greater => BinaryOperator.Greater,
                TokenKind.GreaterEqual => BinaryOperator.GreaterEqual,
                _ => null,
            };
            if (op is null) break;
            Advance();
            Expression right = ParseAdditive();
            left = new BinaryExpr(RangeBetween(left.Range.Start, right.Range.End), op.Value, left, right);
        }
        return left;
    }

    private Expression ParseAdditive() {
        Expression left = ParseMultiplicative();
        while (true) {
            BinaryOperator? op = Current.Kind switch {
                TokenKind.Plus => BinaryOperator.Add,
                TokenKind.Minus => BinaryOperator.Subtract,
                _ => null,
            };
            if (op is null) break;
            Advance();
            Expression right = ParseMultiplicative();
            left = new BinaryExpr(RangeBetween(left.Range.Start, right.Range.End), op.Value, left, right);
        }
        return left;
    }

    private Expression ParseMultiplicative() {
        Expression left = ParseUnary();
        while (true) {
            BinaryOperator? op = Current.Kind switch {
                TokenKind.Star => BinaryOperator.Multiply,
                TokenKind.Slash => BinaryOperator.Divide,
                TokenKind.Percent => BinaryOperator.Modulo,
                _ => null,
            };
            if (op is null) break;
            Advance();
            Expression right = ParseUnary();
            left = new BinaryExpr(RangeBetween(left.Range.Start, right.Range.End), op.Value, left, right);
        }
        return left;
    }

    private Expression ParseUnary() {
        if (Check(TokenKind.Minus) || Check(TokenKind.Bang)) {
            SourceLocation start = Current.Location;
            UnaryOperator op = Current.Kind == TokenKind.Minus ? UnaryOperator.Negate : UnaryOperator.Not;
            Advance();
            Expression operand = ParseUnary();
            return new UnaryExpr(RangeBetween(start, operand.Range.End), op, operand);
        }
        return ParsePostfix();
    }

    private Expression ParsePostfix() {
        Expression e = ParsePrimary();
        while (true) {
            switch (Current.Kind) {
                case TokenKind.Dot: {
                        Advance();
                        Token name = Expect(TokenKind.Identifier, _e2001, "expected member name after '.'");
                        e = new MemberAccessExpr(RangeBetween(e.Range.Start, name.Location), e, name.Lexeme);
                        break;
                    }
                case TokenKind.QuestionDot: {
                        Advance();
                        Token name = Expect(TokenKind.Identifier, _e2001, "expected member name after '?.'");
                        e = new MemberAccessExpr(RangeBetween(e.Range.Start, name.Location), e, name.Lexeme, IsOptional: true);
                        break;
                    }
                case TokenKind.LeftParen: {
                        Advance();
                        List<CallArgument> args = ParseCallArguments();
                        Token rp = Expect(TokenKind.RightParen, _e2001, "expected ')' to close call");
                        e = new CallExpr(RangeBetween(e.Range.Start, rp.Location), e, args);
                        break;
                    }
                case TokenKind.LeftBracket: {
                        Advance();
                        SkipNewlines();
                        Expression idx = ParseExpression();
                        SkipNewlines();
                        Token rb = Expect(TokenKind.RightBracket, _e2001, "expected ']' to close index");
                        e = new IndexExpr(RangeBetween(e.Range.Start, rb.Location), e, idx);
                        break;
                    }
                case TokenKind.Switch: {
                        Advance(); // consume 'switch'
                        Expect(TokenKind.LeftBrace, _e2001, "expected '{' to open switch body");
                        (List<SwitchArm> arms, bool hadRecoveredArm) = ParseSwitchArms();
                        Token rb = Expect(TokenKind.RightBrace, _e2001, "expected '}' to close switch body");
                        e = new SwitchExprNode(RangeBetween(e.Range.Start, rb.Location), e, arms, hadRecoveredArm);
                        break;
                    }
                case TokenKind.LeftBrace when _allowStructLiteral && LooksLikeStructConstruction() && e is IdentifierExpr id: {
                        // TypeName { field: value, … } — named struct construction.
                        // _allowStructLiteral prevents firing in positions where '{' is
                        // unambiguously a block starter (for-in iterable, case patterns).
                        // LooksLikeStructConstruction() prevents firing when the content
                        // does not match 'identifier :' or '}', catching error-recovery paths
                        // such as 'if (a { 1 }' where '{' would otherwise steal the body.
                        e = ParseStructConstruction(e.Range.Start, id.Name);
                        break;
                    }
                default:
                    return e;
            }
        }
    }

    /// <summary>
    /// Parses a named-struct-construction field list — <c>TypeName { field: value, … }</c>
    /// — starting after the leading identifier, with the opening '{' not yet
    /// consumed. Extracted from <see cref="ParsePostfix"/>'s <c>switch</c> to keep
    /// that method's cognitive complexity within the analyzer gate. The field loop
    /// itself is <see cref="ParseBracedFieldInitList"/>, shared verbatim with
    /// <see cref="ParseAnonStructLiteral"/>; only the closing brace's diagnostic
    /// wording and the node built from the result differ between the two forms.
    /// </summary>
    private StructConstructionExpr ParseStructConstruction(SourceLocation start, string typeName) {
        List<FieldInit> fields = ParseBracedFieldInitList();
        Token closeBrace = Expect(TokenKind.RightBrace, _e2001, "expected '}' to close struct construction");
        return new StructConstructionExpr(RangeBetween(start, closeBrace.Location), typeName, fields);
    }

    /// <summary>
    /// Parses the body shared by both brace-delimited field-init lists — the
    /// anonymous-struct literal's <c>#{ … }</c> and the named-struct construction's
    /// <c>TypeName { … }</c> — which are token-for-token identical between the
    /// opening brace and the closing one. The opening brace is consumed here (both
    /// forms open on exactly one token, '#{' or '{'); the closing '}' is left to the
    /// caller so each form keeps its own diagnostic wording. Every field goes through
    /// <see cref="ParseFieldInitOrError"/> (D-405/D-406), not a bare
    /// <see cref="ParseOneFieldInit"/> call, so a malformed field is recovered locally
    /// against this list's own ',' or '}' rather than escaping to the top-level
    /// recovery wrapper. One shared primitive, not two divergent copies — D-406's own
    /// argument applied to its own output.
    /// </summary>
    private List<FieldInit> ParseBracedFieldInitList() {
        Advance(); // consume the opening '{' / '#{'
        SkipNewlines();
        List<FieldInit> fields = [];
        if (!Check(TokenKind.RightBrace)) {
            FieldInit? first = ParseFieldInitOrError();
            if (first is not null) fields.Add(first);
            while (Match(TokenKind.Comma)) {
                SkipNewlines();
                if (Check(TokenKind.RightBrace)) break; // trailing comma
                FieldInit? next = ParseFieldInitOrError();
                if (next is not null) fields.Add(next);
            }
            SkipNewlines();
        }
        return fields;
    }

    // -----------------------------------------------------------------------
    // Switch expression (§3.1) — comma-separated `pattern => result` arms, an
    // optional trailing comma, newlines permitted around separators.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Parses the arm list. Each arm goes through <see cref="ParseSwitchArmOrError"/>
    /// (D-406), not a bare <see cref="ParseSwitchArm"/> call, so a malformed arm is
    /// recovered locally — dropped from the list, not replaced by a placeholder node
    /// — rather than escaping to the top-level recovery wrapper. The second return
    /// value reports whether any arm was dropped this way; the type checker (via
    /// <see cref="SwitchExprNode.HadRecoveredArm"/>) uses it to suppress a derived,
    /// spurious non-exhaustiveness diagnostic when the dropped arm might itself have
    /// been the one carrying exhaustiveness.
    /// </summary>
    private (List<SwitchArm> Arms, bool HadRecoveredArm) ParseSwitchArms() {
        List<SwitchArm> arms = [];
        bool hadRecoveredArm = false;
        SkipNewlines();
        if (Check(TokenKind.RightBrace)) return (arms, hadRecoveredArm);
        SwitchArm? first = ParseSwitchArmOrError();
        if (first is not null) arms.Add(first); else hadRecoveredArm = true;
        while (Match(TokenKind.Comma)) {
            SkipNewlines();
            if (Check(TokenKind.RightBrace)) break; // trailing comma
            SwitchArm? next = ParseSwitchArmOrError();
            if (next is not null) arms.Add(next); else hadRecoveredArm = true;
        }
        SkipNewlines();
        return (arms, hadRecoveredArm);
    }

    /// <summary>
    /// Local recovery wrapper (D-406) around <see cref="ParseSwitchArm"/> — the
    /// switch-expression instance of the D-405 call-site gap. Catching the failure
    /// here keeps <see cref="ParseSwitchArms"/>'s own frame — the one that owns the
    /// switch body's still-open '{' — on the call stack while recovery runs, so
    /// <see cref="SkipToNextLiteralElementBoundary"/> can resynchronise to this
    /// arm list's own ',' or its own '}' instead of a phantom diagnostic being
    /// raised against a leftover '}' by an unrelated, later parse attempt. Returns
    /// <see langword="null"/> for a malformed arm — the arm is omitted from the
    /// switch, not replaced by a placeholder node.
    /// </summary>
    private SwitchArm? ParseSwitchArmOrError() {
        int startPos = _pos;
        try {
            return ParseSwitchArm();
        } catch (ParseFailedException) {
            if (_pos == startPos && !IsAtEnd) {
                Advance();
            }
            SkipToNextLiteralElementBoundary(startPos, TokenKind.Comma);
            return null;
        }
    }

    private SwitchArm ParseSwitchArm() {
        SourceLocation start = Current.Location;
        SwitchPattern pattern = ParseSwitchPattern();
        Expect(TokenKind.Arrow, _e2001, "expected '=>' after switch pattern");
        Expression result = ParseExpression();
        return new SwitchArm(RangeFrom(start), pattern, result);
    }

    private SwitchPattern ParseSwitchPattern() {
        // Catch-all: the identifier '_'.
        if (Check(TokenKind.Identifier) && Current.Lexeme == "_") {
            Token underscore = Advance();
            return new CatchAllPattern(new SourceRange(underscore.Location, underscore.Location));
        }

        // Relational pattern: a leading comparison operator over a constant operand.
        BinaryOperator? relOp = Current.Kind switch {
            TokenKind.Less => BinaryOperator.Less,
            TokenKind.LessEqual => BinaryOperator.LessEqual,
            TokenKind.Greater => BinaryOperator.Greater,
            TokenKind.GreaterEqual => BinaryOperator.GreaterEqual,
            _ => null,
        };
        if (relOp is not null) {
            SourceLocation start = Current.Location;
            Advance();
            // ParseTernary (not ParseExpression) so a `pattern => result` arm is not
            // misread as a lambda — `=>` is the arm arrow, not a lambda body.
            Expression operand = ParseTernary();
            return new RelationalPattern(RangeBetween(start, operand.Range.End), relOp.Value, operand);
        }

        // Value pattern: a constant expression of the scrutinee's type. ParseTernary
        // (not ParseExpression) keeps the following `=>` as the arm arrow rather than a
        // lambda body.
        Expression value = ParseTernary();
        return new ValuePattern(value.Range, value);
    }

    private List<CallArgument> ParseCallArguments() {
        List<CallArgument> args = [];
        SkipNewlines();
        if (Check(TokenKind.RightParen)) return args;
        args.Add(ParseCallArgument());
        while (Match(TokenKind.Comma)) {
            SkipNewlines();
            args.Add(ParseCallArgument());
        }
        SkipNewlines();
        return args;
    }

    private CallArgument ParseCallArgument() {
        SourceLocation start = Current.Location;
        string? name = null;
        if (Check(TokenKind.Identifier) && PeekAt(1).Kind == TokenKind.Colon) {
            name = Current.Lexeme;
            Advance();
            Advance();
        }
        Expression value = ParseExpression();
        return new CallArgument(RangeFrom(start), name, value);
    }

    private Expression ParsePrimary() {
        Token t = Current;
        switch (t.Kind) {
            case TokenKind.IntLiteral: {
                    Advance();
                    long v = ParseIntegerLexeme(t.Lexeme);
                    return new IntLiteralExpr(new SourceRange(t.Location, t.Location), v);
                }
            case TokenKind.FloatLiteral: {
                    Advance();
                    if (!double.TryParse(t.Lexeme, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v)) {
                        throw Fail(_e2001, $"invalid float literal '{t.Lexeme}'");
                    }
                    return new FloatLiteralExpr(new SourceRange(t.Location, t.Location), v);
                }
            case TokenKind.True: {
                    Advance();
                    return new BoolLiteralExpr(new SourceRange(t.Location, t.Location), true);
                }
            case TokenKind.False: {
                    Advance();
                    return new BoolLiteralExpr(new SourceRange(t.Location, t.Location), false);
                }
            case TokenKind.Nil: {
                    Advance();
                    return new NilLiteralExpr(new SourceRange(t.Location, t.Location));
                }
            case TokenKind.RawStringLiteral:
            case TokenKind.RawStringBlockLiteral: {
                    Advance();
                    return new RawStringLiteralExpr(new SourceRange(t.Location, t.Location), StripRawStringDelimiters(t.Lexeme));
                }
            case TokenKind.RegexLiteral: {
                    Advance();
                    (string pattern, string flags) = SplitRegexLexeme(t.Lexeme);
                    return new RegexLiteralExpr(new SourceRange(t.Location, t.Location), pattern, flags);
                }
            case TokenKind.StringStart: return ParseInterpolatedString();
            case TokenKind.Identifier: {
                    // map<K, V>{ ... } — map-literal construction (D-376). 'map' is an
                    // ordinary bindable identifier (map := 5 is legal, D-375's own note),
                    // so a following '<' is ambiguous with the relational comparison
                    // 'map < x'. LooksLikeMapLiteral() is a pure, non-consuming lookahead —
                    // it never calls Advance/Expect/Match — so a failed check leaves _pos
                    // untouched and this falls through to the identifier case unchanged.
                    if (t.Lexeme == "map" && PeekAt(1).Kind == TokenKind.Less && LooksLikeMapLiteral()) {
                        return ParseMapLiteral(t.Location);
                    }
                    Advance();
                    return new IdentifierExpr(new SourceRange(t.Location, t.Location), t.Lexeme);
                }
            case TokenKind.LeftParen: {
                    Advance();
                    SkipNewlines();
                    Expression inner = ParseExpression();
                    SkipNewlines();
                    Token rp = Expect(TokenKind.RightParen, _e2001, "expected ')'");
                    return new GroupingExpr(new SourceRange(t.Location, rp.Location), inner);
                }
            case TokenKind.LeftBracket: return ParseArrayLiteral();
            case TokenKind.HashBrace: return ParseAnonStructLiteral();
            case TokenKind.LeftBrace:
                // A bare '{' in expression position is always a block opener, never a
                // struct literal. Named construction uses 'TypeName {'; anonymous
                // construction uses '#{ }'. Raise E2101 so the user gets the
                // brace-disambiguation hint rather than a generic "unexpected token".
                throw Fail(ErrorCatalog.E2101,
                    "'{' begins a block, not a struct literal. " +
                    "Use '#{ field: value }' for an anonymous struct, " +
                    "or 'TypeName { }' for named construction.");
            default:
                // When line-continuation suppresses the newline after a binary
                // operator (e.g. `a +\n}`), the cursor lands on the anchor while
                // the last consumed token was the dangling operator. Report the
                // error at the operator so the user sees the real failure site.
                if (_pos > 0 && IsBinaryOperatorForContext(_tokens[_pos - 1].Kind)) {
                    Token prev = _tokens[_pos - 1];
                    throw FailAt(prev.Location, _e2001, $"expected expression after '{DescribeToken(prev)}'");
                }
                throw Fail(_e2001, $"unexpected token '{DescribeToken(t)}' — expected expression");
        }
    }

    private ArrayLiteralExpr ParseArrayLiteral() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.LeftBracket, _e2001, "expected '['");
        SkipNewlines();
        List<Expression> elements = [];
        if (!Check(TokenKind.RightBracket)) {
            elements.Add(ParseExpression());
            while (Match(TokenKind.Comma)) {
                SkipNewlines();
                if (Check(TokenKind.RightBracket)) break;
                elements.Add(ParseExpression());
            }
            SkipNewlines();
        }
        Token rb = Expect(TokenKind.RightBracket, _e2001, "expected ']' to close array literal");
        return new ArrayLiteralExpr(new SourceRange(start, rb.Location), elements);
    }

    // #{ field: value, … } — anonymous-struct literal (Sprint 6D).
    // '#{' is a single HashBrace token; the lexer already incremented _depth so
    // the matching '}' is scanned at the correct nesting level. The field loop is
    // ParseBracedFieldInitList, shared with named-struct construction — it consumes
    // the '#{' and runs each field through ParseFieldInitOrError (D-405/D-406), so a
    // malformed field name is recovered locally rather than escaping to the
    // top-level recovery wrapper. 'start' is captured before that call because the
    // helper consumes the opening token.
    private AnonStructExpr ParseAnonStructLiteral() {
        SourceLocation start = Current.Location;
        List<FieldInit> fields = ParseBracedFieldInitList();
        Token closeBrace = Expect(TokenKind.RightBrace, _e2001, "expected '}' to close anonymous struct literal");
        return new AnonStructExpr(new SourceRange(start, closeBrace.Location), fields);
    }

    /// <summary>
    /// Local recovery wrapper (D-405, widened to the named-struct-construction call
    /// site by D-406) around <see cref="ParseOneFieldInit"/>, reached from the field
    /// loop <see cref="ParseBracedFieldInitList"/> that <see cref="ParseAnonStructLiteral"/>
    /// and <see cref="ParseStructConstruction"/> share — both own a still-open '{' and
    /// both reproduced the identical double-diagnostic gap D-405 traced. See
    /// <see cref="ParseMapEntryOrError"/>'s doc comment for the full rationale:
    /// catching the failure at this call site, rather than letting it escape to the
    /// top-level recovery wrapper, keeps the caller's own frame — the one that owns
    /// this literal's still-open '{' — on the call stack while recovery runs, so
    /// <see cref="SkipToNextLiteralElementBoundary"/> can resynchronise to this
    /// literal's own ',' or '}' instead of a phantom diagnostic being raised against
    /// a leftover '}' by an unrelated, later parse attempt.
    /// Returns <see langword="null"/> for a malformed field — the field is
    /// omitted from the literal, not replaced by a placeholder AST node.
    /// </summary>
    private FieldInit? ParseFieldInitOrError() {
        int startPos = _pos;
        try {
            return ParseOneFieldInit();
        } catch (ParseFailedException) {
            if (_pos == startPos && !IsAtEnd) {
                Advance();
            }
            SkipToNextLiteralElementBoundary(startPos, TokenKind.Comma);
            return null;
        }
    }

    // map<K, V>{ "key": value, … } — map-literal construction (D-376). Reached only
    // after LooksLikeMapLiteral() has already confirmed the shape from ParsePrimary,
    // so every Expect below is expected to succeed on well-formed input; a malformed
    // literal (e.g. a missing '}') still fails cleanly through the ordinary
    // Fail()/recovery path like every other literal form. Each entry goes through
    // ParseMapEntryOrError (D-405), not a bare ParseMapEntry call — see that
    // method's doc comment.
    private MapLiteralExpr ParseMapLiteral(SourceLocation start) {
        Advance(); // consume 'map'
        Expect(TokenKind.Less, _e2001, "expected '<' to open map type arguments");
        List<TypeRef> typeArgs = ParseTypeArgumentList();
        TypeRef typeRef = new(RangeFrom(start), "map", typeArgs, IsNullable: false);

        Expect(TokenKind.LeftBrace, _e2001, "expected '{' to open map literal");
        SkipNewlines();
        List<MapEntry> entries = [];
        if (!Check(TokenKind.RightBrace)) {
            MapEntry? first = ParseMapEntryOrError();
            if (first is not null) entries.Add(first);
            while (Match(TokenKind.Comma)) {
                SkipNewlines();
                if (Check(TokenKind.RightBrace)) break; // trailing comma
                MapEntry? next = ParseMapEntryOrError();
                if (next is not null) entries.Add(next);
            }
            SkipNewlines();
        }
        Token closeBrace = Expect(TokenKind.RightBrace, _e2001, "expected '}' to close map literal");
        return new MapLiteralExpr(new SourceRange(start, closeBrace.Location), typeRef, entries);
    }

    /// <summary>
    /// Local recovery wrapper (D-405) around <see cref="ParseMapEntry"/> — the
    /// call-site fix for the double-diagnostic gap traced in that decision.
    /// <see cref="ParseMapLiteral"/>'s entry loop has no recovery point of its
    /// own, so before this fix a malformed key's exception propagated straight
    /// past the still-open '{' up to the nearest top-level recovery wrapper —
    /// several frames further out, by which point <see cref="ParseMapLiteral"/>'s
    /// own frame (the one tracking that this '{' is still open) had already
    /// unwound. <see cref="Synchronise"/> then anchored on the literal's own
    /// closing '}' without consuming it (it cannot distinguish that '}' from a
    /// genuine enclosing block's), leaving it for a fresh, unrelated top-level
    /// parse attempt to fail on again — the phantom second diagnostic.
    /// <para>
    /// Catching the failure here instead keeps <see cref="ParseMapLiteral"/>'s
    /// frame on the stack while recovery runs, so it can resynchronise locally —
    /// via <see cref="SkipToNextLiteralElementBoundary"/>, not the general-purpose
    /// <see cref="Synchronise"/> — to this literal's own next ',' or its own '}'.
    /// <see cref="Synchronise"/> itself is unchanged: it has no anchor for a bare
    /// ',', which is exactly what a comma-separated literal's entry boundary needs
    /// and a statement-level recovery anchor never did.
    /// </para>
    /// <para>
    /// Returns <see langword="null"/> for a malformed entry — the entry is
    /// omitted from the literal (no placeholder <c>MapEntry</c> node), not
    /// replaced. The diagnostic already raised inside <see cref="ParseMapEntry"/>
    /// stays in the bag unchanged; nothing further is added here, so a genuinely
    /// independent second malformed entry later in the same literal still gets
    /// its own diagnostic on the loop's next iteration — this recovers one
    /// element, not the whole literal.
    /// </para>
    /// </summary>
    private MapEntry? ParseMapEntryOrError() {
        int startPos = _pos;
        try {
            return ParseMapEntry();
        } catch (ParseFailedException) {
            if (_pos == startPos && !IsAtEnd) {
                Advance();
            }
            SkipToNextLiteralElementBoundary(startPos, TokenKind.Comma);
            return null;
        }
    }

    /// <summary>
    /// Local synchronisation for one element of a brace-delimited element list —
    /// map-literal entries, anon-struct/named-struct fields and switch-expression
    /// arms (comma-separated, D-405/D-406), plus <c>type</c>-declaration fields and
    /// <c>param</c>-block entries (newline-separated, D-406). Deliberately distinct
    /// from <see cref="Synchronise"/> (§29): that routine anchors on '}', a
    /// top-level declaration keyword, or a newline outside any bracket, none of
    /// which distinguish "the next element" from "the end of this list" inside a
    /// still-open '{' — a bare ',' is not one of its anchors at all, and its
    /// newline anchor cannot tell a literal's own interior newline from a real
    /// statement boundary. This instead stops at the next occurrence of
    /// <paramref name="boundaryToken"/> (<see cref="TokenKind.Comma"/> for a
    /// comma-separated list, <see cref="TokenKind.Newline"/> for a newline-separated
    /// declaration body) or '}' that belongs to the element list itself.
    /// <para>
    /// "Belongs to the element list" is decided by <paramref name="elementStart"/>:
    /// the abandoned element may already have consumed an opening delimiter of its
    /// own before it failed (<c>{ "a": foo(1 2), … }</c> fails inside the argument
    /// list with '(' consumed), so the scan cannot assume it begins at
    /// element-boundary nesting. The delimiters still open at the failure point are
    /// replayed from the element's own tokens first, and the scan then tracks every
    /// bracket-pair kind the rest of that element could open
    /// (<c>(…)</c>, <c>[…]</c>, nested <c>{…}</c>/<c>#{…}</c>, and a string
    /// interpolation's <c>${…}</c>) so a nested literal or call is skipped as a unit
    /// rather than having its own internal ',' or newline mistaken for this list's
    /// separator. A '}' that cannot close whatever is innermost-open is treated as
    /// the enclosing literal's own closing brace and stops the scan unconsumed, so
    /// an element that leaves a bracket permanently open (<c>{ "a": (1, "b": 2 }</c>)
    /// still hands the literal's '}' back to the caller's Expect rather than running
    /// past it. Runs out safely at EOF (no anchor found) rather than looping if the
    /// enclosing literal is itself unterminated.
    /// </para>
    /// <para>
    /// <b>Newline mode also anchors on a top-level declaration keyword</b>
    /// (<see cref="IsTopLevelKeyword"/>), unlike comma mode, which deliberately runs
    /// to true EOF for a literal interior — D-406. A <c>type</c>/<c>param</c> body is
    /// a declaration context ordinarily followed by more top-level declarations in
    /// the same file; without this, one malformed field with an unterminated body
    /// would swallow every later declaration. The keyword check is gated on a
    /// brace-depth counter replayed the same way <see cref="Synchronise"/> gates its
    /// own keyword anchor (plain '{'/'}' only, not the finer per-bracket-kind
    /// <paramref name="elementStart"/> replay above) rather than on the full
    /// delimiter stack being empty: a reserved declaration keyword can never appear
    /// as expression content at any depth (declarations are not expressions), so it
    /// is a reliable anchor even while a paren the field opened is still outstanding
    /// — unlike a bare ',' or '}', which genuinely can occur inside an unterminated
    /// value and needs the full delimiter stack to disambiguate.
    /// </para>
    /// </summary>
    /// <param name="elementStart">
    /// The token index the abandoned element began at, used to replay the
    /// delimiters it left open.
    /// </param>
    /// <param name="boundaryToken">
    /// The element separator this list uses — <see cref="TokenKind.Comma"/> or
    /// <see cref="TokenKind.Newline"/>. The literal's own '}' is always a boundary
    /// in addition to this token.
    /// </param>
    private void SkipToNextLiteralElementBoundary(int elementStart, TokenKind boundaryToken) {
        Stack<TokenKind> open = OpenDelimitersBetween(elementStart, _pos);
        bool anchorOnKeywords = boundaryToken == TokenKind.Newline;
        int localOpenBraces = anchorOnKeywords ? CountOpenPlainBraces(elementStart, _pos) : 0;
        while (!IsAtEnd) {
            TokenKind k = Current.Kind;
            if (anchorOnKeywords && localOpenBraces == 0 && IsTopLevelKeyword(k)) return;
            if (open.Count == 0 && (k == boundaryToken || k == TokenKind.RightBrace)) return;
            if (!TrackDelimiter(open, k) && k == TokenKind.RightBrace) return;
            if (k == TokenKind.LeftBrace) localOpenBraces++;
            else if (k == TokenKind.RightBrace) localOpenBraces--;
            Advance();
        }
    }

    /// <summary>
    /// Replays plain '{'/'}' tokens (not <see cref="TokenKind.HashBrace"/>) over a
    /// token range to seed <see cref="SkipToNextLiteralElementBoundary"/>'s
    /// keyword-anchor gate, mirroring <see cref="Synchronise"/>'s own
    /// <c>localOpenBraces</c> counter exactly — including that routine's existing
    /// choice not to count <c>#{</c> — so the two stay consistent (D-406).
    /// </summary>
    private int CountOpenPlainBraces(int from, int to) {
        int count = 0;
        for (int i = from; i < to; i++) {
            if (_tokens[i].Kind == TokenKind.LeftBrace) count++;
            else if (_tokens[i].Kind == TokenKind.RightBrace) count--;
        }
        return count;
    }

    /// <summary>
    /// Replays the tokens of an abandoned literal element to rebuild the stack of
    /// closing delimiters it still has outstanding at the failure point. Each entry
    /// is the <see cref="TokenKind"/> that would close the corresponding opener.
    /// </summary>
    private Stack<TokenKind> OpenDelimitersBetween(int from, int to) {
        Stack<TokenKind> open = new();
        for (int i = from; i < to; i++) {
            TrackDelimiter(open, _tokens[i].Kind);
        }
        return open;
    }

    /// <summary>
    /// Applies one token to a delimiter stack: pushes the matching closer for an
    /// opener, pops for the closer the innermost opener expects. Returns
    /// <see langword="false"/> only for a closer that does not match what is
    /// innermost-open — the signal <see cref="SkipToNextLiteralElementBoundary"/>
    /// uses to recognise the enclosing literal's own '}'.
    /// </summary>
    private static bool TrackDelimiter(Stack<TokenKind> open, TokenKind k) {
        TokenKind? closer = CloserFor(k);
        if (closer is not null) {
            open.Push(closer.Value);
            return true;
        }
        if (!IsClosingDelimiter(k)) return true;
        if (open.Count == 0 || open.Peek() != k) return false;
        open.Pop();
        return true;
    }

    private static TokenKind? CloserFor(TokenKind k) => k switch {
        TokenKind.LeftParen => TokenKind.RightParen,
        TokenKind.LeftBracket => TokenKind.RightBracket,
        TokenKind.LeftBrace or TokenKind.HashBrace => TokenKind.RightBrace,
        TokenKind.InterpStart => TokenKind.InterpEnd,
        _ => null,
    };

    private static bool IsClosingDelimiter(TokenKind k) =>
        k is TokenKind.RightParen or TokenKind.RightBracket
          or TokenKind.RightBrace or TokenKind.InterpEnd;

    private MapEntry ParseMapEntry() {
        SourceLocation entryStart = Current.Location;
        string key = ParseMapEntryKey();
        SkipNewlines();
        Expect(TokenKind.Colon, _e2001, "expected ':' after map entry key");
        SkipNewlines();
        Expression value = ParseExpression();
        return new MapEntry(RangeBetween(entryStart, value.Range.End), key, value);
    }

    /// <summary>
    /// Parses a map-literal entry's key: a plain double-quoted string literal only —
    /// not raw (backtick), not interpolated. A double-quoted string always lexes as a
    /// <see cref="TokenKind.StringStart"/>/.../<see cref="TokenKind.StringEnd"/> run (there
    /// is no dedicated plain-string token kind, even for a literal with no interpolation
    /// at all), so this parses it via <see cref="ParseInterpolatedString"/> and then
    /// requires every part to be plain text — mirroring the constant-folding check
    /// <c>EvalConstantExpr</c> (<c>Compiler.cs</c>) already uses to recognise a
    /// non-interpolated double-quoted string. Anything else — an identifier, a raw
    /// string, or a genuinely interpolated string — is E2001.
    /// </summary>
    private string ParseMapEntryKey() {
        if (!Check(TokenKind.StringStart)) {
            throw Fail(_e2001, "expected string literal key");
        }
        SourceLocation keyStart = Current.Location;
        Expression keyExpr = ParseInterpolatedString();
        if (keyExpr is InterpolatedStringExpr { Parts: var parts } && parts.All(p => p is StringTextPart)) {
            return string.Concat(parts.OfType<StringTextPart>().Select(p => p.Text));
        }
        throw FailAt(keyStart, _e2001, "expected string literal key");
    }

    private FieldInit ParseOneFieldInit() {
        SourceLocation fieldStart = Current.Location;
        Token nameToken = Expect(TokenKind.Identifier, _e2001, "expected field name");
        SkipNewlines();
        Expect(TokenKind.Colon, _e2001, "expected ':' after field name");
        SkipNewlines();
        Expression fieldValue = ParseExpression();
        return new FieldInit(RangeBetween(fieldStart, fieldValue.Range.End), nameToken.Lexeme, fieldValue);
    }

    private InterpolatedStringExpr ParseInterpolatedString() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.StringStart, _e2001, "expected start of string literal");
        List<StringInterpolationPart> parts = [];
        while (!Check(TokenKind.StringEnd) && !IsAtEnd) {
            Token here = Current;
            if (Match(TokenKind.StringPart)) {
                parts.Add(new StringTextPart(new SourceRange(here.Location, here.Location), here.Lexeme));
                continue;
            }
            if (Match(TokenKind.InterpStart)) {
                Expression inner = ParseExpression();
                Token end = Expect(TokenKind.InterpEnd, _e2001, "expected '}' to close interpolation");
                parts.Add(new StringExpressionPart(new SourceRange(here.Location, end.Location), inner));
                continue;
            }
            throw Fail(_e2001, "unexpected token inside string literal");
        }
        Token close = Expect(TokenKind.StringEnd, _e2001, "expected closing '\"' of string literal");
        return new InterpolatedStringExpr(new SourceRange(start, close.Location), parts);
    }

    // -----------------------------------------------------------------------
    // Lambdas
    // -----------------------------------------------------------------------

    private bool IsLambdaStart() {
        // Identifier => …
        if (Check(TokenKind.Identifier) && PeekAt(1).Kind == TokenKind.Arrow) return true;
        if (!Check(TokenKind.LeftParen)) return false;
        // Scan past the matching ) and check for => (skipping newlines, which
        // line-continuation allows after `=>`).
        int closeIndex = FindMatchingParenIndex(_pos);
        if (closeIndex < 0) return false;
        return NextNonNewlineIs(closeIndex + 1, TokenKind.Arrow);
    }

    private int FindMatchingParenIndex(int from) {
        int depth = 0;
        for (int i = from; i < _tokens.Count; i++) {
            switch (_tokens[i].Kind) {
                case TokenKind.LeftParen:
                    depth++;
                    break;
                case TokenKind.RightParen:
                    depth--;
                    if (depth == 0) return i;
                    break;
                case TokenKind.Eof:
                    return -1;
            }
        }
        return -1;
    }

    /// <summary>
    /// Peeks ahead from the current '{' token to decide whether it opens a
    /// struct-construction body rather than a block. Returns <see langword="true"/>
    /// when the tokens immediately inside look like a field initialiser
    /// (<c>identifier ':'</c>) or an empty construction body (<c>'}'</c>).
    /// Called only when <see cref="_allowStructLiteral"/> is already true.
    /// </summary>
    private bool LooksLikeStructConstruction() {
        int i = _pos + 1; // first token inside the '{'
        while (i < _tokens.Count && _tokens[i].Kind == TokenKind.Newline) i++;
        if (i >= _tokens.Count) return false;
        if (_tokens[i].Kind == TokenKind.RightBrace) return true; // empty body: T { }
        if (_tokens[i].Kind != TokenKind.Identifier) return false;
        // Skip optional newlines between field name and ':' (unusual but handled).
        i++;
        while (i < _tokens.Count && _tokens[i].Kind == TokenKind.Newline) i++;
        if (i >= _tokens.Count) return false;
        return _tokens[i].Kind == TokenKind.Colon;
    }

    /// <summary>
    /// Peeks ahead from the '&lt;' following a 'map' identifier to decide whether it
    /// opens a map-literal type-argument list (<c>map&lt;K, V&gt;{ ... }</c>) rather than
    /// a relational comparison (<c>map &lt; x</c>) on the ordinary bindable identifier
    /// <c>map</c> (D-376). A pure index scan over <c>_tokens[_pos...]</c> — mirrors
    /// <see cref="LooksLikeStructConstruction"/>'s peek-only style: it never calls
    /// <see cref="Advance"/>/<see cref="Expect"/>/<see cref="Match"/>, so it can never
    /// emit a diagnostic and never needs a rewind. Tracks <c>&lt;</c>/<c>&gt;</c> nesting
    /// depth over a token run that looks like an identifier-only type-argument list
    /// (identifiers, optional <c>[</c>/<c>]</c> for an array-typed value argument, the
    /// <c>?</c> nullable suffix D-327's <see cref="ParseTypeRef"/> loop accepts on any
    /// type, and commas) — succeeds only when the matching top-level <c>&gt;</c> is
    /// immediately followed by <c>{</c>. Called only when <c>_pos</c> is at the <c>map</c>
    /// identifier and the following token is already known to be
    /// <see cref="TokenKind.Less"/>.
    /// <para>
    /// <see cref="TokenKind.Colon"/> is deliberately absent from the accepted run: admitting
    /// it alongside <c>?</c> would make <c>map &lt; a ? b : c &gt; { … }</c> — a comparison
    /// feeding a ternary — scan as a map literal. Rejecting <c>:</c> keeps that form an
    /// ordinary expression.
    /// </para>
    /// </summary>
    private bool LooksLikeMapLiteral() {
        int i = _pos + 2; // first token after 'map' '<'
        int depth = 1;
        while (i < _tokens.Count) {
            switch (_tokens[i].Kind) {
                case TokenKind.Less:
                    depth++;
                    i++;
                    break;
                case TokenKind.Greater:
                    depth--;
                    i++;
                    if (depth == 0) {
                        return i < _tokens.Count && _tokens[i].Kind == TokenKind.LeftBrace;
                    }
                    break;
                case TokenKind.Identifier:
                case TokenKind.Comma:
                case TokenKind.LeftBracket:
                case TokenKind.RightBracket:
                case TokenKind.Question:
                    i++;
                    break;
                default:
                    return false;
            }
        }
        return false;
    }

    private bool NextNonNewlineIs(int from, TokenKind kind) {
        for (int j = from; j < _tokens.Count; j++) {
            if (_tokens[j].Kind == TokenKind.Newline) continue;
            return _tokens[j].Kind == kind;
        }
        return false;
    }

    private LambdaExpr ParseLambda() {
        SourceLocation start = Current.Location;
        List<Parameter> parameters = [];
        if (Match(TokenKind.LeftParen)) {
            SkipNewlines();
            if (!Check(TokenKind.RightParen)) {
                parameters.Add(ParseLambdaParameter());
                while (Match(TokenKind.Comma)) {
                    SkipNewlines();
                    parameters.Add(ParseLambdaParameter());
                }
                SkipNewlines();
            }
            Expect(TokenKind.RightParen, _e2001, "expected ')' in lambda parameter list");
        } else {
            Token name = Expect(TokenKind.Identifier, _e2001, "expected lambda parameter name");
            parameters.Add(new Parameter(new SourceRange(name.Location, name.Location), name.Lexeme, null, null));
        }
        Expect(TokenKind.Arrow, _e2001, "expected '=>' in lambda");
        // Block-body lambda: x => { ... }
        if (Check(TokenKind.LeftBrace)) {
            BlockStmt body = ParseBlock();
            return new LambdaExpr(RangeFrom(start), parameters, new LambdaBlockBody(body));
        }
        Expression bodyExpr = ParseExpression();
        return new LambdaExpr(RangeBetween(start, bodyExpr.Range.End), parameters, new LambdaExpressionBody(bodyExpr));
    }

    private Parameter ParseLambdaParameter() {
        SourceLocation start = Current.Location;
        Token name = Expect(TokenKind.Identifier, _e2001, "expected lambda parameter name");
        TypeRef? type = null;
        if (Match(TokenKind.Colon)) {
            type = ParseTypeRef();
        }
        return new Parameter(RangeFrom(start), name.Lexeme, type, null);
    }

    // -----------------------------------------------------------------------
    // Lexeme helpers
    // -----------------------------------------------------------------------

    private long ParseIntegerLexeme(string lexeme) {
        string s = lexeme.Replace("_", "");
        try {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                return Convert.ToInt64(s[2..], 16);
            }
            if (s.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) {
                return Convert.ToInt64(s[2..], 2);
            }
            return long.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        } catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException) {
            throw Fail(_e2001, $"invalid integer literal '{lexeme}'");
        }
    }

    private static string StripRawStringDelimiters(string lexeme) {
        if (lexeme.StartsWith("```", StringComparison.Ordinal) && lexeme.EndsWith("```", StringComparison.Ordinal) && lexeme.Length >= 6) {
            return lexeme[3..^3];
        }
        if (lexeme.Length >= 2 && lexeme[0] == '`' && lexeme[^1] == '`') {
            return lexeme[1..^1];
        }
        return lexeme;
    }

    private static (string pattern, string flags) SplitRegexLexeme(string lexeme) {
        int last = lexeme.LastIndexOf('/');
        if (last <= 0) return (lexeme, "");
        return (lexeme[1..last], lexeme[(last + 1)..]);
    }

    private static string DescribeToken(Token t) {
        if (t.Kind == TokenKind.Newline) return "newline";
        if (t.Kind == TokenKind.Eof) return "end of file";
        if (t.Lexeme.Length == 0) return t.Kind.ToString().ToLowerInvariant();
        return t.Lexeme;
    }

    // -----------------------------------------------------------------------
    // Internal control-flow signal — never escapes the recovery wrappers.
    // -----------------------------------------------------------------------

    private sealed class ParseFailedException : Exception {
        public ParseFailedException(Diagnostic diagnostic) {
            Diagnostic = diagnostic;
        }
        public Diagnostic Diagnostic { get; }
    }
}
