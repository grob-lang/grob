using System.IO;

using Grob.Core;
using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// The <c>path</c> module (D-342): Windows-native path-string decompose/join/normalise
/// operations, built on <see cref="Path"/>. Pure — no capability injection, no throw
/// sites beyond what the type checker already rules out. Extensions are always
/// normalised to the leading-dot lowercase form (<c>".jpg"</c>). Registers exactly the
/// qualified names listed in the compile-time twin, <c>NamespaceRegistry</c>'s
/// <c>path</c> entry in <c>Grob.Compiler</c>.
/// </summary>
public sealed class PathPlugin : IGrobPlugin {
    /// <inheritdoc/>
    public string Name => "path";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);

        registrar.RegisterConstant("path.separator", GrobValue.FromString(Path.DirectorySeparatorChar.ToString()));

        registrar.RegisterNative("path.join", new NativeFunction("path.join", 1, (args, _) =>
            GrobValue.FromString(Path.Combine([.. args.Select(a => a.AsString())]))));

        registrar.RegisterNative("path.joinAll", new NativeFunction("path.joinAll", 1, (args, _) =>
            GrobValue.FromString(Path.Combine([.. args[0].AsArray().Elements.Select(e => e.AsString())]))));

        registrar.RegisterNative("path.extension", new NativeFunction("path.extension", 1, (args, _) =>
            GrobValue.FromString(Path.GetExtension(args[0].AsString()).ToLowerInvariant())));

        registrar.RegisterNative("path.filename", new NativeFunction("path.filename", 1, (args, _) =>
            GrobValue.FromString(Path.GetFileName(args[0].AsString()))));

        registrar.RegisterNative("path.stem", new NativeFunction("path.stem", 1, (args, _) =>
            GrobValue.FromString(Path.GetFileNameWithoutExtension(args[0].AsString()))));

        registrar.RegisterNative("path.directory", new NativeFunction("path.directory", 1, (args, _) =>
            GrobValue.FromString(Path.GetDirectoryName(args[0].AsString()) ?? string.Empty)));

        registrar.RegisterNative("path.resolve", new NativeFunction("path.resolve", 1, (args, _) => {
            // Path.GetFullPath("") throws ArgumentException — treat an empty input as
            // "." (the current directory) rather than letting a raw .NET exception
            // escape the native-call boundary (CodeRabbit review, PR #130).
            string p = args[0].AsString();
            return GrobValue.FromString(Path.GetFullPath(p.Length == 0 ? "." : p));
        }));

        registrar.RegisterNative("path.normalise", new NativeFunction("path.normalise", 1, (args, _) =>
            GrobValue.FromString(Normalise(args[0].AsString()))));

        registrar.RegisterNative("path.isAbsolute", new NativeFunction("path.isAbsolute", 1, (args, _) =>
            GrobValue.FromBool(Path.IsPathRooted(args[0].AsString()))));

        registrar.RegisterNative("path.isRelative", new NativeFunction("path.isRelative", 1, (args, _) =>
            GrobValue.FromBool(!Path.IsPathRooted(args[0].AsString()))));

        registrar.RegisterNative("path.changeExtension", new NativeFunction("path.changeExtension", 2, (args, _) =>
            GrobValue.FromString(Path.ChangeExtension(args[0].AsString(), args[1].AsString()))));
    }

    /// <summary>
    /// Collapses <c>.</c> and <c>..</c> segments and fixes separators without anchoring a
    /// relative input to the current working directory — the one thing
    /// <see cref="Path.GetFullPath(string)"/> cannot do, since it always absolutises.
    /// The drive/root prefix (if any) is preserved and never popped by a <c>..</c>.
    /// </summary>
    private static string Normalise(string path) {
        string root = Path.GetPathRoot(path) ?? string.Empty;
        string remainder = path[root.Length..];
        string[] segments = remainder.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

        var collapsed = new List<string>();
        foreach (string segment in segments.Where(segment => segment != ".")) {
            if (segment == ".." && collapsed.Count > 0 && collapsed[^1] != "..") {
                collapsed.RemoveAt(collapsed.Count - 1);
                continue;
            }
            collapsed.Add(segment);
        }

        string normalisedRoot = root.Length == 0
            ? string.Empty
            : root.Replace('/', Path.DirectorySeparatorChar);
        return normalisedRoot + string.Join(Path.DirectorySeparatorChar, collapsed);
    }
}
