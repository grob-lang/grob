using Grob.Runtime;

namespace Grob.Stdlib.Tests;

/// <summary>
/// A settable <see cref="IClock"/> test double — the DAG forbids <c>Grob.Stdlib.Tests</c>
/// from referencing <c>Grob.Cli</c>, where the production <c>SystemClock</c> lives.
/// </summary>
internal sealed class TestClock : IClock {
    internal DateTime Now { get; set; }

    internal TestClock(DateTime now) => Now = now;

    public DateTime UtcNow => Now;
}
