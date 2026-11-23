using Grob.Core;

namespace Grob.Cli;

/// <summary>
/// The E9001 report (D-451 Decision 2): what the CLI prints, on stderr, when a phase throws an
/// exception that is neither a script's <c>exit(n)</c> nor a Grob runtime error. It carries the
/// title from <see cref="ErrorCatalog"/>, the phase and the bug-report line, and no host stack
/// trace unless <c>--verbose</c> is set (requirements §1).
/// </summary>
/// <remarks>
/// D-077 keeps script values out of error output, and a host exception's message can carry one
/// (<c>TimeZoneNotFoundException</c> echoes the user's zone string). So the message is printed
/// only for a <see cref="GrobInternalException"/>, which Grob writes, and any other exception
/// is named by its type alone. <c>--verbose</c> adds the exception's full text.
/// </remarks>
internal static class InternalErrorReport {
    private const string BugReportLine =
        "this is a bug in Grob, not in your script; please report it with the script at https://github.com/grob-lang/grob/issues";

    /// <summary>Writes the report for <paramref name="exception"/>, thrown in <paramref name="phase"/> of a run of <paramref name="file"/>.</summary>
    public static void Write(TextWriter stderr, string file, PipelinePhase phase, Exception exception, bool verbose) {
        stderr.WriteLine($"error[{ErrorCatalog.E9001.Code}]: {ErrorCatalog.E9001.Title}");
        stderr.WriteLine($"  --> {file} (while {Words(phase)})");
        stderr.WriteLine(exception is GrobInternalException
            ? $"  = {nameof(GrobInternalException)}: {exception.Message}"
            : $"  = {exception.GetType().Name}");
        stderr.WriteLine($"  = {BugReportLine}");
        if (verbose) {
            stderr.WriteLine(exception.ToString());
        }
    }

    /// <summary>
    /// The exit code for an internal error in <paramref name="phase"/>: <c>2</c> before the script
    /// ran, which is true, and <c>1</c> while it ran, when it may have had side effects. Both are
    /// inside the documented set; a dedicated code would not be.
    /// </summary>
    public static int ExitCodeFor(PipelinePhase phase) =>
        phase == PipelinePhase.Running ? ExitCodes.RuntimeError : ExitCodes.CompileError;

    private static string Words(PipelinePhase phase) => phase switch {
        PipelinePhase.LoadingStandardLibrary => "loading the standard library",
        PipelinePhase.Lexing => "lexing",
        PipelinePhase.Parsing => "parsing",
        PipelinePhase.TypeChecking => "type checking",
        PipelinePhase.Compiling => "compiling",
        _ => "running",
    };
}
