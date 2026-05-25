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
    private const string E2001 = "E2001";

    private readonly IReadOnlyList<Token> _tokens;
    private readonly DiagnosticBag _diagnostics;
    private int _pos;

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

    private Token Expect(TokenKind k, string code, string message) {
        if (Check(k)) {
            return Advance();
        }
        throw Fail(code, message);
    }

    // -----------------------------------------------------------------------
    // Failure / diagnostics
    // -----------------------------------------------------------------------

    private ParseFailedException Fail(string code, string message) {
        SourceRange range = new(Current.Location, Current.Location);
        Diagnostic d = new(code, message, range, Severity.Error);
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
    // Synchronise (§29). Anchors:
    //   * Newline at BracketDepth == 0 and no locally-opened brace.
    //   * }  closing an enclosing block (not one we opened inside the skip).
    //   * Top-level declaration keywords (fn/type/param/import/const/readonly).
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
        return IsTopLevelKeyword(k);
    }

    private static bool IsTopLevelKeyword(TokenKind k) =>
        k is TokenKind.Fn or TokenKind.Type or TokenKind.Param
          or TokenKind.Import or TokenKind.Const or TokenKind.Readonly;

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
            return new ErrorDecl(RangeFrom(start), ex.Diagnostic);
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
            return new ErrorStmt(RangeFrom(start), ex.Diagnostic);
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
            return new ErrorExpr(RangeFrom(start), ex.Diagnostic);
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
        TokenKind.Param => ParseParamBlockDecl(),
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
        Expect(TokenKind.Import, E2001, "expected 'import'");
        Token name = Expect(TokenKind.Identifier, E2001, "expected module name after 'import'");
        string modulePath = name.Lexeme;
        // Dotted path: foo.bar.baz
        while (Match(TokenKind.Dot)) {
            Token seg = Expect(TokenKind.Identifier, E2001, "expected identifier after '.' in module path");
            modulePath = modulePath + "." + seg.Lexeme;
        }
        string? alias = null;
        if (Match(TokenKind.As)) {
            Token a = Expect(TokenKind.Identifier, E2001, "expected alias name after 'as'");
            alias = a.Lexeme;
        }
        return new ImportDecl(RangeFrom(start), modulePath, alias);
    }

    private ConstDecl ParseConstDecl(bool alreadyConsumedKeyword) {
        SourceLocation start = alreadyConsumedKeyword ? PeekAt(-1).Location : Current.Location;
        if (!alreadyConsumedKeyword) {
            Expect(TokenKind.Const, E2001, "expected 'const'");
        }
        Token name = Expect(TokenKind.Identifier, E2001, "expected name after 'const'");
        TypeRef? annotatedType = null;
        if (Match(TokenKind.Colon)) {
            annotatedType = ParseTypeRef();
        }
        Expect(TokenKind.ColonAssign, E2001, "expected ':=' in const declaration");
        Expression value = ParseExpression();
        return new ConstDecl(RangeFrom(start), name.Lexeme, annotatedType, value);
    }

    private ReadonlyDecl ParseReadonlyDecl() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.Readonly, E2001, "expected 'readonly'");
        Token name = Expect(TokenKind.Identifier, E2001, "expected name after 'readonly'");
        TypeRef? annotatedType = null;
        if (Match(TokenKind.Colon)) {
            annotatedType = ParseTypeRef();
        }
        Expect(TokenKind.ColonAssign, E2001, "expected ':=' in readonly declaration");
        Expression value = ParseExpression();
        return new ReadonlyDecl(RangeFrom(start), name.Lexeme, annotatedType, value);
    }

    private FnDecl ParseFnDecl() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.Fn, E2001, "expected 'fn'");
        Token name = Expect(TokenKind.Identifier, E2001, "expected function name after 'fn'");
        Expect(TokenKind.LeftParen, E2001, "expected '(' in function declaration");
        List<Parameter> parameters = ParseParameterList(TokenKind.RightParen);
        Expect(TokenKind.RightParen, E2001, "expected ')' to close parameter list");
        Expect(TokenKind.Colon, E2001, "expected ':' followed by return type");
        TypeRef returnType = ParseTypeRef();
        BlockStmt body = ParseBlock();
        return new FnDecl(RangeFrom(start), name.Lexeme, parameters, returnType, body);
    }

    private TypeDecl ParseTypeDecl() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.Type, E2001, "expected 'type'");
        Token name = Expect(TokenKind.Identifier, E2001, "expected type name after 'type'");
        Expect(TokenKind.LeftBrace, E2001, "expected '{' to open type body");
        SkipNewlines();
        List<TypeField> fields = [];
        while (!Check(TokenKind.RightBrace) && !IsAtEnd) {
            fields.Add(ParseTypeField());
            SkipNewlines();
        }
        Expect(TokenKind.RightBrace, E2001, "expected '}' to close type body");
        return new TypeDecl(RangeFrom(start), name.Lexeme, fields);
    }

    private TypeField ParseTypeField() {
        SourceLocation start = Current.Location;
        Token name = Expect(TokenKind.Identifier, E2001, "expected field name");
        Expect(TokenKind.Colon, E2001, "expected ':' after field name");
        TypeRef type = ParseTypeRef();
        Expression? defaultValue = null;
        if (Match(TokenKind.Assign)) {
            defaultValue = ParseExpression();
        }
        return new TypeField(RangeFrom(start), name.Lexeme, type, defaultValue);
    }

    private ParamBlockDecl ParseParamBlockDecl() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.Param, E2001, "expected 'param'");
        Expect(TokenKind.LeftBrace, E2001, "expected '{' to open param block");
        SkipNewlines();
        List<Parameter> parameters = [];
        while (!Check(TokenKind.RightBrace) && !IsAtEnd) {
            parameters.Add(ParseDeclaredParameter());
            SkipNewlines();
        }
        Expect(TokenKind.RightBrace, E2001, "expected '}' to close param block");
        return new ParamBlockDecl(RangeFrom(start), parameters);
    }

    private Parameter ParseDeclaredParameter() {
        // Decorators (@allowed, @minLength, …) are tokens at this point; we
        // consume them as opaque sequences in v1 — the type checker handles
        // their semantic content later.
        SourceLocation start = Current.Location;
        SkipParameterDecorators();
        Token name = Expect(TokenKind.Identifier, E2001, "expected parameter name");
        Expect(TokenKind.Colon, E2001, "expected ':' after parameter name");
        TypeRef type = ParseTypeRef();
        Expression? defaultValue = null;
        if (Match(TokenKind.Assign)) {
            defaultValue = ParseExpression();
        }
        return new Parameter(RangeFrom(start), name.Lexeme, type, defaultValue);
    }

    private void SkipParameterDecorators() {
        while (Match(TokenKind.At)) {
            Expect(TokenKind.Identifier, E2001, "expected decorator name after '@'");
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
        Expect(TokenKind.RightParen, E2001, "expected ')' to close decorator arguments");
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
    // Type references
    // -----------------------------------------------------------------------

    private TypeRef ParseTypeRef() {
        SourceLocation start = Current.Location;
        Token name = Expect(TokenKind.Identifier, E2001, "expected type name");
        List<TypeRef> args = [];
        if (Match(TokenKind.Less)) {
            args.Add(ParseTypeRef());
            while (Match(TokenKind.Comma)) {
                args.Add(ParseTypeRef());
            }
            Expect(TokenKind.Greater, E2001, "expected '>' to close generic arguments");
        }
        bool nullable = Match(TokenKind.Question);
        return new TypeRef(RangeFrom(start), name.Lexeme, args, nullable);
    }

    // -----------------------------------------------------------------------
    // Statements
    // -----------------------------------------------------------------------

    private BlockStmt ParseBlock() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.LeftBrace, E2001, "expected '{'");
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
        Expect(TokenKind.RightBrace, E2001, "expected '}'");
        return new BlockStmt(RangeFrom(start), stmts);
    }

    private Statement ParseStatement() {
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
            case TokenKind.Select: return ParseSelect();
            case TokenKind.Const: return ParseConstDeclAsStatement();
            default: return ParseExpressionOrAssignmentStatement();
        }
    }

    private Statement ParseConstDeclAsStatement() {
        // Top-level wraps `const` as a Declaration. Block-level const lands in
        // Sprint 2 (\u00a724); for now we consume the declaration so recovery picks
        // up past it, then report it as a parse error.
        _ = ParseConstDecl(false);
        throw Fail(E2001, "'const' is only allowed at the top level in Sprint 1");
    }

    private IfStmt ParseIf() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.If, E2001, "expected 'if'");
        Expression cond = ParseExpression();
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
        Expect(TokenKind.While, E2001, "expected 'while'");
        Expression cond = ParseExpression();
        BlockStmt body = ParseBlock();
        return new WhileStmt(RangeFrom(start), cond, body);
    }

    private ForInStmt ParseForIn() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.For, E2001, "expected 'for'");
        Token first = Expect(TokenKind.Identifier, E2001, "expected loop variable after 'for'");
        List<string> vars = [first.Lexeme];
        if (Match(TokenKind.Comma)) {
            Token second = Expect(TokenKind.Identifier, E2001, "expected second loop variable after ','");
            vars.Add(second.Lexeme);
        }
        Expect(TokenKind.In, E2001, "expected 'in' in for-loop header");
        Expression iterable = ParseIterable();
        BlockStmt body = ParseBlock();
        return new ForInStmt(RangeFrom(start), vars, iterable, body);
    }

    private Expression ParseIterable() {
        SourceLocation start = Current.Location;
        Expression iter = ParseExpression();
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
        Expect(TokenKind.Return, E2001, "expected 'return'");
        Expression? value = null;
        if (!Check(TokenKind.Newline) && !Check(TokenKind.RightBrace) && !IsAtEnd) {
            value = ExpressionOrError();
        }
        return new ReturnStmt(RangeFrom(start), value);
    }

    private TryStmt ParseTry() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.Try, E2001, "expected 'try'");
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
            } else if (Check(TokenKind.Identifier)) {
                // (Type) — type-only catch
                exceptionType = ParseTypeRef();
            }
            Expect(TokenKind.RightParen, E2001, "expected ')' to close catch clause header");
        }
        BlockStmt body = ParseBlock();
        return new CatchClause(RangeFrom(start), exceptionType, exceptionVar, body);
    }

    private SelectStmt ParseSelect() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.Select, E2001, "expected 'select'");
        Expression subject = ParseExpression();
        Expect(TokenKind.LeftBrace, E2001, "expected '{' to open select body");
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
            Expect(TokenKind.Case, E2001, "expected 'case' or 'default'");
            List<Expression> patterns = [ParseExpression()];
            while (Match(TokenKind.Comma)) {
                patterns.Add(ParseExpression());
            }
            BlockStmt body = ParseBlock();
            cases.Add(new CaseClause(RangeFrom(cs), patterns, body));
            SkipNewlines();
        }
        Expect(TokenKind.RightBrace, E2001, "expected '}' to close select body");
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
                Expect(TokenKind.ColonAssign, E2001, "expected ':=' after declared type");
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
            Expect(TokenKind.Colon, E2001, "expected ':' in ternary expression");
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
                        Token name = Expect(TokenKind.Identifier, E2001, "expected member name after '.'");
                        e = new MemberAccessExpr(RangeBetween(e.Range.Start, name.Location), e, name.Lexeme);
                        break;
                    }
                case TokenKind.QuestionDot: {
                        Advance();
                        Token name = Expect(TokenKind.Identifier, E2001, "expected member name after '?.'");
                        // ?. is represented as MemberAccess in Sprint 1; the lowering
                        // to a nil-guard expression lands with the type checker.
                        e = new MemberAccessExpr(RangeBetween(e.Range.Start, name.Location), e, name.Lexeme);
                        break;
                    }
                case TokenKind.LeftParen: {
                        Advance();
                        List<CallArgument> args = ParseCallArguments();
                        Token rp = Expect(TokenKind.RightParen, E2001, "expected ')' to close call");
                        e = new CallExpr(RangeBetween(e.Range.Start, rp.Location), e, args);
                        break;
                    }
                case TokenKind.LeftBracket: {
                        Advance();
                        SkipNewlines();
                        Expression idx = ParseExpression();
                        SkipNewlines();
                        Token rb = Expect(TokenKind.RightBracket, E2001, "expected ']' to close index");
                        e = new IndexExpr(RangeBetween(e.Range.Start, rb.Location), e, idx);
                        break;
                    }
                default:
                    return e;
            }
        }
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
                        throw Fail(E2001, $"invalid float literal '{t.Lexeme}'");
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
                    Advance();
                    return new IdentifierExpr(new SourceRange(t.Location, t.Location), t.Lexeme);
                }
            case TokenKind.LeftParen: {
                    Advance();
                    SkipNewlines();
                    Expression inner = ParseExpression();
                    SkipNewlines();
                    Token rp = Expect(TokenKind.RightParen, E2001, "expected ')'");
                    return new GroupingExpr(new SourceRange(t.Location, rp.Location), inner);
                }
            case TokenKind.LeftBracket: return ParseArrayLiteral();
            default:
                throw Fail(E2001, $"unexpected token '{DescribeToken(t)}' — expected expression");
        }
    }

    private ArrayLiteralExpr ParseArrayLiteral() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.LeftBracket, E2001, "expected '['");
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
        Token rb = Expect(TokenKind.RightBracket, E2001, "expected ']' to close array literal");
        return new ArrayLiteralExpr(new SourceRange(start, rb.Location), elements);
    }

    private InterpolatedStringExpr ParseInterpolatedString() {
        SourceLocation start = Current.Location;
        Expect(TokenKind.StringStart, E2001, "expected start of string literal");
        List<StringInterpolationPart> parts = [];
        while (!Check(TokenKind.StringEnd) && !IsAtEnd) {
            Token here = Current;
            if (Match(TokenKind.StringPart)) {
                parts.Add(new StringTextPart(new SourceRange(here.Location, here.Location), here.Lexeme));
                continue;
            }
            if (Match(TokenKind.InterpStart)) {
                Expression inner = ParseExpression();
                Token end = Expect(TokenKind.InterpEnd, E2001, "expected '}' to close interpolation");
                parts.Add(new StringExpressionPart(new SourceRange(here.Location, end.Location), inner));
                continue;
            }
            throw Fail(E2001, "unexpected token inside string literal");
        }
        Token close = Expect(TokenKind.StringEnd, E2001, "expected closing '\"' of string literal");
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
            Expect(TokenKind.RightParen, E2001, "expected ')' in lambda parameter list");
        } else {
            Token name = Expect(TokenKind.Identifier, E2001, "expected lambda parameter name");
            parameters.Add(new Parameter(new SourceRange(name.Location, name.Location), name.Lexeme, null, null));
        }
        Expect(TokenKind.Arrow, E2001, "expected '=>' in lambda");
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
        Token name = Expect(TokenKind.Identifier, E2001, "expected lambda parameter name");
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
            throw Fail(E2001, $"invalid integer literal '{lexeme}'");
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
