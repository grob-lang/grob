namespace Grob.Runtime;

/// <summary>
/// The capability interface for the file system (D-319's name, R-32). It carries one member
/// today: the current directory <c>path.resolve</c> anchors a relative path to. The 9C
/// <c>fs</c> increment grows it. An OS-backed implementation reads the process's working
/// directory; a test or embedding host (the playground) supplies a fixed one, so no native
/// reads the host's working directory behind the host's back.
/// </summary>
public interface IFileSystem {
    /// <summary>The absolute directory a relative path resolves against.</summary>
    string CurrentDirectory { get; }
}
