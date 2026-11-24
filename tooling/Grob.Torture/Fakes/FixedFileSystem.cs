using Grob.Runtime;

namespace Grob.Torture.Fakes;

/// <summary>
/// A file system whose current directory is a fixed path that need not exist, so
/// <c>path.resolve</c> never anchors to the machine's working directory. 9C extends it when
/// <see cref="IFileSystem"/> grows.
/// </summary>
internal sealed class FixedFileSystem : IFileSystem {
    /// <summary>
    /// The current directory every fake file system reports: a directory at the root of the
    /// current drive, which the oracle never reads or creates.
    /// </summary>
    public static readonly string Root = Path.GetFullPath("/grob-torture");

    /// <inheritdoc/>
    public string CurrentDirectory => Root;
}
