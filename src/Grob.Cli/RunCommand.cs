using Grob.Compiler;
using Grob.Compiler.Ast;
using Grob.Core;
using Grob.Runtime;
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
/// Exit-code conventions (<see cref="ExitCodes"/>; requirements §8):
/// <list type="bullet">
///   <item><description><c>0</c> — clean run.</description></item>
///   <item><description><c>1</c> — a Grob runtime error, or an internal error while the script was running.</description></item>
///   <item><description><c>2</c> — a compile-time diagnostic, or an internal error before the script ran.</description></item>
///   <item><description><c>3</c> — a usage error, including a file that cannot be read.</description></item>
///   <item><description><c>n</c> — the code the script passed to <c>exit(n)</c>.</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class RunCommand {
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;
    private readonly TextReader _stdin;
    private readonly bool _verbose;
    private readonly Func<HostCapabilities> _capabilities;
    private readonly Action<PipelinePhase>? _phaseHook;

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
    public RunCommand(TextWriter stdout, TextWriter stderr, TextReader? stdin = null, bool verbose = false)
        : this(stdout, stderr, stdin, verbose, HostCapabilities.System) { }

    /// <summary>
    /// The capabilities seam (D-428 Decision 3): as the public constructor, but the
    /// catalogue each <see cref="Run"/> builds is over the capabilities
    /// <paramref name="capabilities"/> returns, called once per run.
    /// </summary>
    /// <param name="stdout">Writer for programme output (<c>print()</c>).</param>
    /// <param name="stderr">Writer for diagnostics and error messages.</param>
    /// <param name="stdin">Source for <c>input()</c>; <see langword="null"/> means a closed stream.</param>
    /// <param name="verbose">Selects <c>log.*</c>'s initial threshold.</param>
    /// <param name="capabilities">Builds the random source, environment and clock for one run.</param>
    /// <param name="phaseHook">
    /// Called as each phase of a run starts (gate report §2.5). A test seam: a hook that throws
    /// makes that phase fail on demand, so the E9001 report is tested without an open defect's
    /// reproducer. <see langword="null"/> in production.
    /// </param>
    internal RunCommand(TextWriter stdout, TextWriter stderr, TextReader? stdin, bool verbose,
            Func<HostCapabilities> capabilities, Action<PipelinePhase>? phaseHook = null) {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        ArgumentNullException.ThrowIfNull(capabilities);
        _stdout = stdout;
        _stderr = stderr;
        _stdin = stdin ?? TextReader.Null;
        _verbose = verbose;
        _capabilities = capabilities;
        _phaseHook = phaseHook;
    }

    /// <summary>
    /// Compiles and executes the <c>.grob</c> file at <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the script file.</param>
    /// <returns>The process exit code, as the class remarks list.</returns>
    public int Run(string filePath) {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!TryReadSource(filePath, out string source)) {
            return ExitCodes.UsageError;
        }

        // The one place that wraps the whole pipeline, from the catalogue build to the end of the
        // run: the first frame that owns every phase, and the only one that knows which phase
        // failed. src/CLAUDE.md allows catching Exception only at the outermost CLI entry point,
        // and this is it for `grob run`. A script's exit and a Grob runtime error keep their own
        // paths and are caught first, so the general clause cannot swallow either.
        var phase = new PhaseTracker(_phaseHook);
        try {
            return Execute(filePath, source, phase);
        } catch (GrobExitException exitEx) {
            return exitEx.Code;
        } catch (GrobRuntimeException runtimeEx) {
            DiagnosticFormatter.WriteRuntime(runtimeEx, filePath, _stderr);
            return ExitCodes.RuntimeError;
        } catch (Exception ex) {
            InternalErrorReport.Write(_stderr, filePath, phase.Current, ex, _verbose);
            return InternalErrorReport.ExitCodeFor(phase.Current);
        }
    }

    // A file that cannot be read is a usage error: the caller named a path the CLI cannot use,
    // and nothing was compiled.
    private bool TryReadSource(string filePath, out string source) {
        source = string.Empty;
        try {
            source = File.ReadAllText(filePath);
            return true;
        } catch (FileNotFoundException) {
            _stderr.WriteLine($"error: file not found: {filePath}");
        } catch (DirectoryNotFoundException) {
            _stderr.WriteLine($"error: file not found: {filePath}");
        } catch (UnauthorizedAccessException) {
            _stderr.WriteLine($"error: cannot read file (access denied): {filePath}");
        } catch (IOException ex) {
            _stderr.WriteLine($"error: cannot read file: {ex.Message}");
        }
        return false;
    }

    private int Execute(string filePath, string source, PhaseTracker phase) {
        // One catalogue per run, shared by the type checker and the VM (D-426 Decision 2).
        // Building it before the check is safe: no plugin touches a capability at
        // registration (gate report §1.3), so a script that fails to type-check costs no
        // clock read, environment read or random draw.
        phase.Enter(PipelinePhase.LoadingStandardLibrary);
        var streams = new TwoWriterStreams(_stdout, _stderr, _stdin);
        NativeCatalogue catalogue = PluginRegistration.BuildCatalogue(streams, _verbose, _capabilities());

        var bag = new DiagnosticBag();
        phase.Enter(PipelinePhase.Lexing);
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag, filePath);
        phase.Enter(PipelinePhase.Parsing);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        phase.Enter(PipelinePhase.TypeChecking);
        new TypeChecker(bag, catalogue).Check(unit);

        if (bag.HasErrors) {
            DiagnosticFormatter.Write(bag, _stderr);
            return ExitCodes.CompileError;
        }

        phase.Enter(PipelinePhase.Compiling);
        Chunk chunk = GrobCompiler.Compile(unit, bag);

        if (bag.HasErrors) {
            DiagnosticFormatter.Write(bag, _stderr);
            return ExitCodes.CompileError;
        }

        phase.Enter(PipelinePhase.Running);
        new VirtualMachine(streams, catalogue).Run(chunk);
        return ExitCodes.Success;
    }
}
