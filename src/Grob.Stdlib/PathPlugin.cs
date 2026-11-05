using System.IO;

using Grob.Core;
using Grob.Runtime;

using static Grob.Stdlib.Signatures;

namespace Grob.Stdlib;

/// <summary>
/// The <c>path</c> module (D-342): Windows-native path-string decompose/join/normalise
/// operations, built on <see cref="Path"/>. Pure — no capability injection, no throw
/// sites beyond what the type checker already rules out. Extensions are always
/// normalised to the leading-dot lowercase form (<c>".jpg"</c>). Each native registers
/// with its signature, which the type checker reads from the catalogue (D-426).
/// </summary>
public sealed class PathPlugin : IGrobPlugin {
    /// <inheritdoc/>
    public string Name => "path";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);

        registrar.RegisterConstant("path.separator", GrobValue.FromString(Path.DirectorySeparatorChar.ToString()));

        registrar.RegisterNative(Native("path.join", StringType, Variadic("parts", StringType)),
            new NativeFunction("path.join", 1, (args, _) =>
                GrobValue.FromString(Path.Combine([.. args.Select(a => a.AsString())]))));

        registrar.RegisterNative(Native("path.joinAll", StringType, Param("parts", StringArrayType)),
            new NativeFunction("path.joinAll", 1, (args, _) =>
                GrobValue.FromString(Path.Combine([.. args[0].AsArray().Elements.Select(e => e.AsString())]))));

        registrar.RegisterNative(Native("path.extension", StringType, Param("p", StringType)),
            new NativeFunction("path.extension", 1, (args, _) =>
                GrobValue.FromString(Path.GetExtension(args[0].AsString()).ToLowerInvariant())));

        registrar.RegisterNative(Native("path.filename", StringType, Param("p", StringType)),
            new NativeFunction("path.filename", 1, (args, _) =>
                GrobValue.FromString(Path.GetFileName(args[0].AsString()))));

        registrar.RegisterNative(Native("path.stem", StringType, Param("p", StringType)),
            new NativeFunction("path.stem", 1, (args, _) =>
                GrobValue.FromString(Path.GetFileNameWithoutExtension(args[0].AsString()))));

        registrar.RegisterNative(Native("path.directory", StringType, Param("p", StringType)),
            new NativeFunction("path.directory", 1, (args, _) =>
                GrobValue.FromString(Path.GetDirectoryName(args[0].AsString()) ?? string.Empty)));

        registrar.RegisterNative(Native("path.resolve", StringType, Param("p", StringType)),
            new NativeFunction("path.resolve", 1, (args, _) => {
                // Path.GetFullPath("") throws ArgumentException — treat an empty input as
                // "." (the current directory) rather than letting a raw .NET exception
                // escape the native-call boundary (CodeRabbit review, PR #130).
                string p = args[0].AsString();
                return GrobValue.FromString(Path.GetFullPath(p.Length == 0 ? "." : p));
            }));

        registrar.RegisterNative(Native("path.normalise", StringType, Param("p", StringType)),
            new NativeFunction("path.normalise", 1, (args, _) =>
                GrobValue.FromString(Normalise(args[0].AsString()))));

        registrar.RegisterNative(Native("path.isAbsolute", BoolType, Param("p", StringType)),
            new NativeFunction("path.isAbsolute", 1, (args, _) =>
                GrobValue.FromBool(Path.IsPathRooted(args[0].AsString()))));

        registrar.RegisterNative(Native("path.isRelative", BoolType, Param("p", StringType)),
            new NativeFunction("path.isRelative", 1, (args, _) =>
                GrobValue.FromBool(!Path.IsPathRooted(args[0].AsString()))));

        registrar.RegisterNative(Native("path.changeExtension", StringType, Param("p", StringType), Param("ext", StringType)),
            new NativeFunction("path.changeExtension", 2, (args, _) =>
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
