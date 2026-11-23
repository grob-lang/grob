namespace Grob.Torture;

/// <summary>
/// The bounds on one run (gate report §4.4). The VM is bounded by a cancellation token it
/// checks once per budget of steps (D-319), so its dispatch loop gains no check. The token
/// does not reach the lexer, parser, checker or compiler, so the whole run is also bounded by
/// a watchdog of <see cref="Vm"/> plus <see cref="Compile"/>.
/// </summary>
/// <param name="Vm">How long the VM may run before its token is cancelled.</param>
/// <param name="Compile">The allowance for the phases before the VM, added to <paramref name="Vm"/> for the watchdog.</param>
internal sealed record OracleBudget(TimeSpan Vm, TimeSpan Compile) {
    /// <summary>The CI slice's bound: 250 ms of VM time, measured against a 50 ms warm worst case.</summary>
    public static OracleBudget Ci { get; } = new(TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(2));

    /// <summary>The watchdog: how long a run may take before it is a timeout finding.</summary>
    public TimeSpan Watchdog => Vm + Compile;
}
