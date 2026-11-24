namespace Grob.Torture;

/// <summary>The class of a finding, the second field of every <see cref="Signature"/>.</summary>
internal enum FindingClass {
    /// <summary>An exception from any phase: a <c>GrobInternalException</c> or any host exception.</summary>
    Exception,

    /// <summary>An <c>exit(n)</c> with <c>n</c> outside <c>0..255</c>, a P1 under the strategy's §1.</summary>
    ExitRange,

    /// <summary>A diagnostic or runtime error whose code is not in <c>ErrorCatalog.All</c>.</summary>
    Unregistered,

    /// <summary>A diagnostic whose location is outside the source.</summary>
    Location,

    /// <summary>An identifier left without a <c>Declaration</c>, or with sentinels that disagree (§3.1.1).</summary>
    Sentinel,

    /// <summary>A run that did not end within its watchdog: a phase that ignored its bound.</summary>
    Timeout,

    /// <summary>
    /// A campaign worker died while running the program, as a stack overflow (R-70) or an
    /// out-of-memory ends a process; its journal names the program and the phase (gate report §4.4).
    /// </summary>
    ProcessDeath,
}
