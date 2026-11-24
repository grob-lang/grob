namespace Grob.Cli;

/// <summary>
/// The phase of a run the CLI is in. An E9001 report names it, and its exit code follows from
/// it: a failure before <see cref="Running"/> means no statement of the script ran (D-451
/// Decision 5).
/// </summary>
internal enum PipelinePhase {
    /// <summary>Building the stdlib catalogue.</summary>
    LoadingStandardLibrary,

    /// <summary>Scanning the source into tokens.</summary>
    Lexing,

    /// <summary>Parsing the tokens into a compilation unit.</summary>
    Parsing,

    /// <summary>Type checking the compilation unit.</summary>
    TypeChecking,

    /// <summary>Compiling the checked unit to a chunk.</summary>
    Compiling,

    /// <summary>Running the chunk in the VM.</summary>
    Running,
}
