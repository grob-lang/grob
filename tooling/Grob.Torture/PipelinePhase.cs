namespace Grob.Torture;

/// <summary>
/// The phase of the pipeline a run is in. A finding names the phase it arose in, so a
/// failure after the checker reads as the checker accepting a program a later phase cannot
/// run, and a failure before it as a lexer or parser defect (crash-oracle prompt §3).
/// </summary>
internal enum PipelinePhase {
    /// <summary>Building the stdlib catalogue over the run's capabilities.</summary>
    Catalogue,

    /// <summary>Scanning the source into tokens.</summary>
    Lex,

    /// <summary>Parsing the tokens into a compilation unit.</summary>
    Parse,

    /// <summary>Type checking the compilation unit.</summary>
    Check,

    /// <summary>Compiling the checked unit to a chunk.</summary>
    Compile,

    /// <summary>Running the chunk in the VM.</summary>
    Run,
}
