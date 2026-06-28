using System.Globalization;
using System.Reflection;
using System.Text;

using Grob.Compiler;
using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;
using Grob.Vm;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Cli;

/// <summary>
/// Implements the <c>grob repl</c> command: an interactive read-eval-print loop
/// backed by the full Grob pipeline (lex → parse → type-check → compile → VM).
/// </summary>
/// <remarks>
/// <para><b>Persistent session scope.</b> All mutable (<c>:=</c>) and
/// <c>readonly</c> bindings declared in previous entries are visible in later
/// entries within the same session.  The session state is maintained in the
/// VM's globals table (<see cref="VirtualMachine.Globals"/>).  Before each new
/// entry is compiled, a <em>preamble</em> of re-declarations is synthesised
/// from the current globals snapshot so the type checker and compiler know
/// about previously declared names.  <c>const</c> bindings are inlined by the
/// compiler and never enter the globals table; they therefore do <em>not</em>
/// persist across entry boundaries — use <c>readonly</c> for cross-entry
/// constants.  <c>readonly</c> immutability is a compile-time check scoped to
/// the single entry in which the binding is declared; it is not re-enforced in
/// later entries.</para>
/// <para><b>Expression auto-print rule.</b> If an entry parses to exactly one
/// <see cref="ExpressionStmt"/> whose wrapped expression is not a call to a
/// known side-effecting built-in (<c>print</c>, <c>exit</c>), the result is
/// auto-printed.  Declarations and multi-statement entries are silent.</para>
/// <para><b>Multi-line input.</b> After each line the source is scanned; if the
/// bracket depth (<c>{ }</c>, <c>( )</c>, <c>[ ]</c>) is greater than zero a
/// continuation prompt (<c>..&gt;</c>) is shown and reading continues until the
/// brackets balance.</para>
/// <para><b>History.</b> Input is read via <see cref="TextReader.ReadLine"/>.
/// Interactive sessions receive OS-level terminal history (up/down recall on
/// Windows).  Tests supply a <see cref="System.IO.StringReader"/>.</para>
/// <para><b>Stream conventions.</b> Programme output (<c>print()</c> from
/// scripts) and auto-printed expression results go to stdout.  Diagnostics and
/// errors go to stderr.  The REPL does not exit on an error — it reports and
/// prompts again.</para>
/// </remarks>
public sealed class ReplCommand {
    private readonly TextReader _input;
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    // The VM is persistent across entries so that globals accumulate.
    // Stack.Reset() is called by vm.Run() at the start of each chunk.
    private readonly VirtualMachine _vm;

    // Maps each session-global name to its declared GrobType.
    // Required so that nil-valued nullable globals can be re-declared with
    // the correct type annotation in the synthesised preamble
    // (e.g. "name: string? := nil" rather than "name := nil", which the
    // type checker would reject on a later reference needing a string?).
    private readonly Dictionary<string, GrobType> _sessionTypes =
        new(StringComparer.Ordinal);

    // Virtual source-file name used in all diagnostics emitted for REPL entries.
    private const string ReplSource = "<repl>";

    /// <summary>
    /// Initialises a <see cref="ReplCommand"/> that reads from
    /// <paramref name="input"/> and writes output and errors to the supplied writers.
    /// </summary>
    /// <param name="input">Source of user input. Use <see cref="Console.In"/> for
    /// interactive sessions; a <see cref="System.IO.StringReader"/> in tests.</param>
    /// <param name="stdout">Writer for programme output and auto-printed values.</param>
    /// <param name="stderr">Writer for diagnostics and error messages.</param>
    public ReplCommand(TextReader input, TextWriter stdout, TextWriter stderr) {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        _input = input;
        _stdout = stdout;
        _stderr = stderr;
        _vm = new VirtualMachine(stdout);
    }

    /// <summary>
    /// Runs the REPL loop until <c>exit</c> is typed or EOF is reached.
    /// </summary>
    /// <returns>Always returns <c>0</c>.</returns>
    public int Run() {
        string version = ParseVersion(typeof(ReplCommand).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion);
        _stdout.WriteLine($"Grob {version}  |  type 'exit' to quit");
        _stdout.WriteLine();

        while (true) {
            string? entry = ReadEntry();
            if (entry is null) break;   // EOF

            string trimmed = entry.Trim();
            if (trimmed.Length == 0) continue;
            if (trimmed == "exit") break;
            if (trimmed == "help") {
                PrintHelp();
                continue;
            }

            ProcessEntry(entry);
        }

        return 0;
    }

    // -----------------------------------------------------------------------
    // Entry reading — prompt, continuation, multi-line detection
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads one logical entry from the input, expanding to multiple physical
    /// lines when the bracket depth is non-zero after the first line.
    /// Returns <c>null</c> on EOF.
    /// </summary>
    private string? ReadEntry() {
        _stdout.Write("G> ");
        _stdout.Flush();

        string? first = _input.ReadLine();
        if (first is null) return null;

        var sb = new StringBuilder(first);

        // Continue reading as long as the accumulated source has unbalanced brackets.
        while (IsIncompleteSource(sb.ToString())) {
            _stdout.Write("..> ");
            _stdout.Flush();
            string? more = _input.ReadLine();
            if (more is null) break;    // EOF mid-block — let the compiler diagnose it
            sb.Append('\n');
            sb.Append(more);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="source"/> has more open-bracket
    /// tokens than matching close-bracket tokens, signalling that the entry is
    /// syntactically incomplete and a continuation line should be read.
    /// </summary>
    private static bool IsIncompleteSource(string source) {
        var bag = new DiagnosticBag();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag, ReplSource);
        return HasOpenBrackets(tokens);
    }

    /// <summary>
    /// Counts open vs. close bracket tokens and returns <c>true</c> when the
    /// token stream has more openers than closers (i.e. brackets are unbalanced).
    /// Only <see cref="TokenKind.LeftBrace"/>, <see cref="TokenKind.LeftParen"/>
    /// and <see cref="TokenKind.LeftBracket"/> are counted; interpolation starters
    /// use dedicated token kinds and are excluded.
    /// </summary>
    private static bool HasOpenBrackets(IReadOnlyList<Token> tokens) {
        int depth = 0;
        foreach (Token t in tokens) {
            switch (t.Kind) {
                case TokenKind.LeftBrace:
                case TokenKind.LeftParen:
                case TokenKind.LeftBracket:
                    depth++;
                    break;
                case TokenKind.RightBrace:
                case TokenKind.RightParen:
                case TokenKind.RightBracket:
                    if (depth > 0) depth--;
                    break;
            }
        }
        return depth > 0;
    }

    // -----------------------------------------------------------------------
    // Entry processing — compile, execute, auto-print
    // -----------------------------------------------------------------------

    /// <summary>
    /// Compiles and executes one REPL entry against the persistent session scope,
    /// auto-printing the result when the entry is a bare expression.
    /// </summary>
    private void ProcessEntry(string entrySrc) {
        // 1. Detect bare-expression before building the combined source so the
        //    detection sees only the user's entry, not the synthesised preamble.
        bool autoPrint = IsBareExpression(entrySrc);

        // 2. Build a preamble that re-declares every session global with its
        //    current value so the type checker knows about previous declarations.
        string preamble = BuildPreamble();
        string fullSrc = preamble.Length > 0
            ? preamble + "\n" + entrySrc
            : entrySrc;

        // 3. Full pipeline.
        var bag = new DiagnosticBag();
        IReadOnlyList<Token> tokens = Lexer.Scan(fullSrc, bag, ReplSource);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);

        if (bag.HasErrors) {
            DiagnosticFormatter.Write(bag, _stderr);
            return;
        }

        Chunk chunk = GrobCompiler.Compile(unit, bag);

        if (bag.HasErrors) {
            DiagnosticFormatter.Write(bag, _stderr);
            return;
        }

        // 4. Patch Pop → Print for bare-expression entries so the value is
        //    automatically printed to stdout.  The compiler emits:
        //      …expression bytecode… Pop Return
        //    for all non-print ExpressionStmt nodes.  chunk.Count - 2 is the
        //    Pop byte; chunk.Count - 1 is the Return byte.
        if (autoPrint && chunk.Count >= 2 &&
            chunk.ReadByte(chunk.Count - 2) == (byte)OpCode.Pop) {
            chunk.PatchByte(chunk.Count - 2, (byte)OpCode.Print);
        }

        // 5. Execute against the persistent VM.
        try {
            _vm.Run(chunk);
        } catch (GrobRuntimeException runtimeEx) {
            DiagnosticFormatter.WriteRuntime(runtimeEx, ReplSource, _stderr);
            return;     // report and continue — the REPL session is not over
        }

        // 6. Register newly declared globals so the preamble for the next
        //    entry carries the right type annotations (especially for nil-
        //    valued nullable declarations).
        RegisterNewDeclarations(entrySrc);
    }

    // -----------------------------------------------------------------------
    // Bare-expression detection
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns <c>true</c> when <paramref name="entrySrc"/> parses to exactly
    /// one top-level <see cref="ExpressionStmt"/> whose wrapped expression is
    /// not a call to a side-effecting built-in (<c>print</c>, <c>exit</c>).
    /// </summary>
    private static bool IsBareExpression(string entrySrc) {
        var bag = new DiagnosticBag();
        IReadOnlyList<Token> tokens = Lexer.Scan(entrySrc, bag, ReplSource);
        CompilationUnit unit = Parser.Parse(tokens, bag);

        if (unit.TopLevel.Count != 1) return false;
        if (unit.TopLevel[0] is not ExpressionStmt exprStmt) return false;

        // Built-in side-effecting calls are not auto-printed.
        if (exprStmt.Expression is CallExpr call &&
            call.Callee is IdentifierExpr { Name: "print" or "exit" }) {
            return false;
        }

        return true;
    }

    // -----------------------------------------------------------------------
    // Preamble synthesis
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a Grob source snippet that re-declares every known session global
    /// with its current runtime value.  The snippet is prepended to each new
    /// entry so the type checker sees all previously declared names.
    /// </summary>
    /// <remarks>
    /// Nil-valued globals require a nullable type annotation (e.g.
    /// <c>name: string? := nil</c>); the annotation is taken from
    /// <see cref="_sessionTypes"/> which is populated by
    /// <see cref="RegisterNewDeclarations"/>.  For all other globals the value
    /// is rendered as a literal (<see cref="ValueToLiteral"/>).
    /// </remarks>
    private string BuildPreamble() {
        if (_vm.Globals.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        foreach (KeyValuePair<string, GrobValue> kv in _vm.Globals) {
            string name = kv.Key;
            GrobValue value = kv.Value;

            if (value.IsNil) {
                // Need a type annotation so the type checker accepts the nil.
                string ann = _sessionTypes.TryGetValue(name, out GrobType t)
                    ? NullableAnnotation(t)
                    : "int?";    // defensive fallback; should not be reached
                sb.Append(name).Append(": ").Append(ann).Append(" := nil\n");
            } else {
                sb.Append(name).Append(" := ").Append(ValueToLiteral(value)).Append('\n');
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Scans <paramref name="entrySrc"/> for new variable declarations and
    /// records their <see cref="GrobType"/> in <see cref="_sessionTypes"/> so
    /// future preamble synthesis can emit the correct nullable annotations.
    /// Only declarations whose names are not already tracked are registered
    /// (re-declaration errors are handled by the type checker, not here).
    /// </summary>
    private void RegisterNewDeclarations(string entrySrc) {
        var bag = new DiagnosticBag();
        IReadOnlyList<Token> tokens = Lexer.Scan(entrySrc, bag, ReplSource);
        CompilationUnit unit = Parser.Parse(tokens, bag);

        foreach (AstNode node in unit.TopLevel) {
            switch (node) {
                case VarDeclStmt v when !_sessionTypes.ContainsKey(v.Name): {
                        GrobType type = v.AnnotatedType is not null
                            ? TypeRefToGrobType(v.AnnotatedType)
                            : InferTypeFromGlobal(v.Name);
                        _sessionTypes[v.Name] = type;
                        break;
                    }
                case ReadonlyDecl r when !_sessionTypes.ContainsKey(r.Name):
                    _sessionTypes[r.Name] = InferTypeFromGlobal(r.Name);
                    break;

                    // ConstDecl values are inlined by the compiler; they are
                    // never stored in vm.Globals and do not persist across entries.
                    // See class-level remarks for the documented REPL limitation.
            }
        }
    }

    // -----------------------------------------------------------------------
    // Type helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Converts a syntactic <see cref="TypeRef"/> to the corresponding
    /// <see cref="GrobType"/>, applying the nullable modifier when set.
    /// </summary>
    private static GrobType TypeRefToGrobType(TypeRef typeRef) {
        GrobType baseType = typeRef.Name switch {
            "int" => GrobType.Int,
            "float" => GrobType.Float,
            "string" => GrobType.String,
            "bool" => GrobType.Bool,
            _ => GrobType.Unknown,
        };
        return typeRef.IsNullable ? GrobTypeHelpers.ToNullable(baseType) : baseType;
    }

    /// <summary>
    /// Returns the <see cref="GrobType"/> for a session global by inspecting
    /// its current runtime value in <see cref="VirtualMachine.Globals"/>.
    /// </summary>
    private GrobType InferTypeFromGlobal(string name) {
        if (!_vm.Globals.TryGetValue(name, out GrobValue value)) return GrobType.Unknown;
        return value.Kind switch {
            GrobValueKind.Int => GrobType.Int,
            GrobValueKind.Float => GrobType.Float,
            GrobValueKind.String => GrobType.String,
            GrobValueKind.Bool => GrobType.Bool,
            _ => GrobType.Unknown,
        };
    }

    /// <summary>
    /// Returns the nullable type-annotation string (e.g. <c>"string?"</c>) for
    /// a given <see cref="GrobType"/>.  Used when synthesising preamble entries
    /// for nil-valued session globals.
    /// </summary>
    private static string NullableAnnotation(GrobType type) => type switch {
        GrobType.NullableInt => "int?",
        GrobType.NullableFloat => "float?",
        GrobType.NullableString => "string?",
        GrobType.NullableBool => "bool?",
        _ => "int?",    // defensive fallback
    };

    // -----------------------------------------------------------------------
    // Value-to-source-literal conversion
    // -----------------------------------------------------------------------

    /// <summary>
    /// Renders a <see cref="GrobValue"/> as a valid Grob source literal that,
    /// when compiled and executed, produces the same value.
    /// </summary>
    private static string ValueToLiteral(GrobValue value) => value.Kind switch {
        GrobValueKind.Int => value.AsInt().ToString(CultureInfo.InvariantCulture),
        GrobValueKind.Float => FormatFloat(value.AsFloat()),
        GrobValueKind.String => EscapeStringLiteral(value.AsString()),
        GrobValueKind.Bool => value.AsBool() ? "true" : "false",
        _ => "nil",
    };

    /// <summary>
    /// Formats a <see cref="double"/> as a Grob float literal that the lexer
    /// will parse back as a <c>float</c> (never as an <c>int</c>).
    /// </summary>
    private static string FormatFloat(double d) {
        // Use round-trip format to preserve full precision.
        string s = d.ToString("R", CultureInfo.InvariantCulture);
        // Ensure the literal contains a decimal point or exponent so the Grob
        // lexer treats it as a float, not an integer.
        return s.Contains('.') || s.Contains('E') || s.Contains('e')
            ? s
            : s + ".0";
    }

    /// <summary>
    /// Returns a double-quoted Grob string literal with all special characters
    /// escaped.  Escape sequences recognised by the Grob lexer:
    /// <c>\n \r \t \\ \" \$</c>.
    /// </summary>
    private static string EscapeStringLiteral(string value) {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (char c in value) {
            sb.Append(c switch {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '$' => "\\$",
                _ => c.ToString(),
            });
        }
        sb.Append('"');
        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // Version string helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the version label to display in the REPL banner.
    /// Strips the <c>+commitHash</c> suffix that MinVer appends to pre-release
    /// strings, and falls back to <c>"unknown"</c> when the attribute is absent.
    /// </summary>
    internal static string ParseVersion(string? informational) =>
        informational is null ? "unknown" : informational.Split('+')[0];

    // -----------------------------------------------------------------------
    // Help text
    // -----------------------------------------------------------------------

    private void PrintHelp() {
        _stdout.WriteLine("Grob interactive session");
        _stdout.WriteLine();
        _stdout.WriteLine("  Type any Grob expression or statement and press Enter.");
        _stdout.WriteLine("  Bare expressions print their result automatically.");
        _stdout.WriteLine("  Multi-line blocks: open a '{' and keep typing — '..>' marks continuation.");
        _stdout.WriteLine();
        _stdout.WriteLine("  exit    End the session");
        _stdout.WriteLine("  help    Show this message");
    }
}
