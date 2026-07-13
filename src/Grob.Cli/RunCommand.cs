using Grob.Compiler;
using Grob.Compiler.Ast;
using Grob.Core;
using Grob.Vm;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Cli;

/// <summary>
/// Implements the <c>grob run &lt;file&gt;</c> command. Reads a <c>.grob</c>
/// source file, runs it through the full pipeline (lex → parse → type-check →
/// compile → VM execute), and returns the process exit code.
/// </summary>
/// <remarks>
/// <para>
/// Stream conventions (personality doc):
/// <list type="bullet">
///   <item><description>Program output (from <c>print()</c>) → <c>stdout</c>.</description></item>
///   <item><description>Diagnostics and errors → <c>stderr</c>.</description></item>
/// </list>
/// </para>
/// <para>
/// Exit-code conventions:
/// <list type="bullet">
///   <item><description><c>0</c> — clean run.</description></item>
///   <item><description>Non-zero — compile-time or runtime error; or <c>exit(n)</c> from the script.</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class RunCommand {
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;
    private readonly TextReader _stdin;
    private readonly bool _verbose;

    /// <summary>
    /// Initialises a <see cref="RunCommand"/> that writes program output to
    /// <paramref name="stdout"/>, diagnostics to <paramref name="stderr"/>, and reads
    /// <c>input()</c> (Sprint 8 Increment C) from <paramref name="stdin"/>.
    /// </summary>
    /// <param name="stdout">Writer for programme output (<c>print()</c>).</param>
    /// <param name="stderr">Writer for diagnostics and error messages.</param>
    /// <param name="stdin">
    /// Source for <c>input()</c>. Optional and defaults to <see cref="TextReader.Null"/>
    /// (a closed stream — <c>ReadLine()</c> returns <see langword="null"/> immediately,
    /// raising the catchable <c>IoError</c>) so the many existing two-argument call sites
    /// across the test suite, none of which exercise <c>input()</c>, need no change —
    /// mirrors D-343's same call-site-preserving default for <c>SingleWriterStreams</c>.
    /// </param>
    /// <param name="verbose">
    /// Selects <c>log.*</c>'s initial threshold (<c>--verbose</c> on the CLI): <c>true</c>
    /// starts at <c>LogLevel.Debug</c>, <c>false</c> (the default) at <c>LogLevel.Info</c>.
    /// </param>
    public RunCommand(TextWriter stdout, TextWriter stderr, TextReader? stdin = null, bool verbose = false) {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        _stdout = stdout;
        _stderr = stderr;
        _stdin = stdin ?? TextReader.Null;
        _verbose = verbose;
    }

    /// <summary>
    /// Compiles and executes the <c>.grob</c> file at <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the script file.</param>
    /// <returns>
    /// Process exit code: <c>0</c> on success; non-zero on any error; or the
    /// code passed to <c>exit(n)</c> by the script.
    /// </returns>
    public int Run(string filePath) {
        ArgumentNullException.ThrowIfNull(filePath);

        string source;
        try {
            source = File.ReadAllText(filePath);
        } catch (FileNotFoundException) {
            _stderr.WriteLine($"error: file not found: {filePath}");
            return 1;
        } catch (DirectoryNotFoundException) {
            _stderr.WriteLine($"error: file not found: {filePath}");
            return 1;
        } catch (UnauthorizedAccessException) {
            _stderr.WriteLine($"error: cannot read file (access denied): {filePath}");
            return 1;
        } catch (IOException ex) {
            _stderr.WriteLine($"error: cannot read file: {ex.Message}");
            return 1;
        }

        var bag = new DiagnosticBag();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag, filePath);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);

        if (bag.HasErrors) {
            DiagnosticFormatter.Write(bag, _stderr);
            return 1;
        }

        Chunk chunk = GrobCompiler.Compile(unit, bag);

        if (bag.HasErrors) {
            DiagnosticFormatter.Write(bag, _stderr);
            return 1;
        }

        try {
            var streams = new TwoWriterStreams(_stdout, _stderr, _stdin);
            var vm = new VirtualMachine(streams);
            PluginRegistration.RegisterAll(vm, new SystemRandomSource(), new SystemEnvironment(), streams, _verbose);
            vm.Run(chunk);
            return 0;
        } catch (GrobExitException exitEx) {
            return exitEx.Code;
        } catch (GrobRuntimeException runtimeEx) {
            DiagnosticFormatter.WriteRuntime(runtimeEx, filePath, _stderr);
            return 1;
        } catch (GrobInternalException internalEx) {
            // Internal errors indicate a compiler or VM invariant violation.
            // Report as a user-facing message without exposing the host stack
            // trace (D-039). Include the message for diagnostics.
            WriteInternalError(internalEx);
            return 1;
        }
    }

    /// <summary>
    /// Writes a user-facing message for an internal invariant violation.
    /// </summary>
    /// <remarks>
    /// This path is unreachable under correct compiler and VM operation —
    /// all reachable invariant violations are prevented by the type checker
    /// before this point. Excluded from coverage measurement.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(
        Justification = "Defensive handler for unreachable invariant violations.")]
    private void WriteInternalError(GrobInternalException ex) {
        _stderr.WriteLine($"error: internal error: {ex.Message}");
        _stderr.WriteLine("Please report this as a bug at https://github.com/grob-lang/grob/issues");
    }

}
