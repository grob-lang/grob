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

    /// <summary>
    /// Initialises a <see cref="RunCommand"/> that writes program output to
    /// <paramref name="stdout"/> and diagnostics to <paramref name="stderr"/>.
    /// </summary>
    public RunCommand(TextWriter stdout, TextWriter stderr) {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        _stdout = stdout;
        _stderr = stderr;
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
            var vm = new VirtualMachine(_stdout);
            vm.Run(chunk);
            return 0;
        } catch (GrobExitException exitEx) {
            return exitEx.Code;
        } catch (GrobRuntimeException runtimeEx) {
            DiagnosticFormatter.WriteRuntime(runtimeEx, filePath, _stderr);
            return 1;
        }
    }
}
