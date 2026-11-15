using Grob.Runtime;

namespace Grob.Cli;

/// <summary>
/// The composition root's <see cref="IFileSystem"/> implementation (R-32). Reads the
/// process's working directory on every access, so <c>path.resolve</c> sees what it saw
/// before the capability existed.
/// </summary>
internal sealed class SystemFileSystem : IFileSystem {
    public string CurrentDirectory => Environment.CurrentDirectory;
}
