namespace Grob.Torture;

/// <summary>
/// How one run ended (gate report §1.1). Every kind but <see cref="Finding"/> is allowed.
/// </summary>
internal enum OutcomeKind {
    /// <summary>Diagnostics, each with a registered code and a location inside the source.</summary>
    Diagnostics,

    /// <summary>The program compiled and ran to its end.</summary>
    Clean,

    /// <summary>The program called <c>exit(n)</c> with <c>n</c> in <c>0..255</c>.</summary>
    Exit,

    /// <summary>A Grob runtime error with a registered code.</summary>
    RuntimeError,

    /// <summary>
    /// The VM honoured the run's cancellation at its bound: counted, not a finding, because
    /// the VM responded (ruling Q1). A legal <c>while (true) {}</c> ends so.
    /// </summary>
    Bounded,

    /// <summary>At least one finding; the run's <see cref="OracleResult.Findings"/> holds them.</summary>
    Finding,
}
