namespace Grob.DriftCheck;

/// <summary>
/// Resolves the canonical corpus locations by walking up from the running
/// assembly to the repository root. The gate is stateless and reads the live
/// documents from <c>docs/design/</c> and the wiki from <c>docs/wiki/ADR/</c>;
/// the walk-up makes it robust to however deep the build output directory sits.
/// Mirrors the resolution used by the D-308 <c>ErrorCatalogAgreementTests</c>.
/// </summary>
public static class RepoPaths {
    /// <summary>The design corpus directory: <c>docs/design</c> under the repo root.</summary>
    public static string DesignDir => Path.Combine(RepoRoot(), "docs", "design");

    /// <summary>The published ADR directory: <c>docs/wiki/ADR</c> under the repo root.</summary>
    public static string AdrDir => Path.Combine(RepoRoot(), "docs", "wiki", "ADR");

    /// <summary>The repository root, holding <c>docs/</c>, <c>src/</c> and <c>tests/</c>.</summary>
    /// <returns>The absolute path of the repository root.</returns>
    public static string RepoRoot() {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null) {
            // The design corpus is the unambiguous marker of the repo root.
            if (Directory.Exists(Path.Combine(dir.FullName, "docs", "design"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the repository root (a directory containing docs/design) " +
            $"by walking up from {AppContext.BaseDirectory}. The consistency gate needs the " +
            "canonical corpus on disk.");
    }

    /// <summary>The error-code registry file.</summary>
    public static string ErrorCodes => Path.Combine(DesignDir, "grob-error-codes.md");

    /// <summary>The decisions log file.</summary>
    public static string DecisionsLog => Path.Combine(DesignDir, "grob-decisions-log.md");

    /// <summary>The v1 requirements file (holds §3.3 opcode and §3.4 token listings).</summary>
    public static string Requirements => Path.Combine(DesignDir, "grob-v1-requirements.md");
}
