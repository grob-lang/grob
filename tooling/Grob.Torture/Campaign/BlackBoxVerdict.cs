namespace Grob.Torture.Campaign;

/// <summary>What a user would see from a reproducer through the real CLI.</summary>
internal enum BlackBoxVerdict {
    /// <summary>The CLI reported E9001 with no stack trace.</summary>
    Reported,

    /// <summary>stderr held a .NET stack frame or the unhandled-exception banner.</summary>
    StackTrace,

    /// <summary>The CLI ran it without an internal error.</summary>
    NotReproduced,

    /// <summary>The CLI did not end within the runner's timeout.</summary>
    TimedOut,
}
