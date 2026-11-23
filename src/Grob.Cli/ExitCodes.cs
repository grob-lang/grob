namespace Grob.Cli;

/// <summary>
/// The process exit codes the CLI itself produces: the documented set (requirements §8; the
/// adversarial strategy's §1; D-451 Decision 5). A script's own <c>exit(n)</c> passes <c>n</c>
/// through and is not in this set.
/// </summary>
internal static class ExitCodes {
    /// <summary>A clean run.</summary>
    public const int Success = 0;

    /// <summary>A Grob runtime error, or an internal error while the script was running.</summary>
    public const int RuntimeError = 1;

    /// <summary>A compile-time diagnostic, or an internal error before the script ran.</summary>
    public const int CompileError = 2;

    /// <summary>A usage error: an unknown command, a missing argument or a file that cannot be read.</summary>
    public const int UsageError = 3;
}
