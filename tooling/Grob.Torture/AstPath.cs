using System.Collections;
using System.Reflection;

using Grob.Compiler.Ast;

namespace Grob.Torture;

/// <summary>
/// Finds where a node sits in a tree, by reflection over each node's syntactic children: the
/// properties its public constructors take, so the checker's annotations, which can point back
/// up the tree, are never followed. Used only on a violation, so its cost never touches a clean
/// run.
/// </summary>
internal static class AstPath {
    /// <summary>The name used when <c>RoleOf</c> cannot find the node.</summary>
    public const string Unknown = "unknown";

    private static readonly string AstNamespace = typeof(AstNode).Namespace!;

    /// <summary>
    /// The node's role: the owning type and property of its parent and of the node itself, as
    /// <c>Parent.Property/Owner.Property</c>; <see cref="Unknown"/> when the node is not in the tree.
    /// </summary>
    public static string RoleOf(AstNode root, AstNode target) {
        var path = new List<string>();
        return Search(root, target, path) ? string.Join('/', path.TakeLast(2)) : Unknown;
    }

    private static bool Search(object node, AstNode target, List<string> path) {
        if (ReferenceEquals(node, target)) return true;

        Type type = node.GetType();
        foreach (PropertyInfo property in SyntacticChildren(type)) {
            path.Add($"{type.Name}.{property.Name}");
            if (property.GetValue(node) is { } value && SearchValue(value, target, path)) return true;
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }

    private static bool SearchValue(object value, AstNode target, List<string> path) => value switch {
        _ when IsAstObject(value) => Search(value, target, path),
        IEnumerable items and not string => items.OfType<object>().Any(item => IsAstObject(item) && Search(item, target, path)),
        _ => false,
    };

    internal static bool IsAstObject(object value) =>
        value.GetType().Namespace?.StartsWith(AstNamespace, StringComparison.Ordinal) == true;

    internal static IEnumerable<PropertyInfo> SyntacticChildren(Type type) {
        var names = type.GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => names.Contains(p.Name));
    }
}
