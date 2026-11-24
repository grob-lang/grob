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

    /// <summary>
    /// The long mode's bound for a campaign's programs: 2 s of VM time (gate report §4.4), so a
    /// program that is slow but ends is not cut off as it would be at the CI slice's bound.
    /// </summary>
    public static OracleBudget Long { get; } = new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

    /// <summary>
    /// The bound on a minimising candidate: 25 ms of VM time. Deleting lines often deletes a loop's
    /// update, and at the CI slice's 250 ms those candidates made minimising four times slower,
    /// measured over a campaign of 200, for the same reproducers. A finding that arises later in
    /// a run than this is not minimised, and its record keeps the program whole.
    /// </summary>
    public static OracleBudget Minimise { get; } = new(TimeSpan.FromMilliseconds(25), TimeSpan.FromSeconds(2));

    /// <summary>The watchdog: how long a run may take before it is a timeout finding.</summary>
    public TimeSpan Watchdog => Vm + Compile;
}
