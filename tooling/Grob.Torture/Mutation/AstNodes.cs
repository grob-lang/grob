using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Expressions;
using Grob.Core;

namespace Grob.Torture.Mutation;

/// <summary>A node of the tree with the node that holds it.</summary>
/// <param name="Node">An <see cref="AstNode"/> or one of the AST's helper records: a parameter, a call argument, a type reference, a field initialiser.</param>
/// <param name="Parent">The enclosing node, or <see langword="null"/> for the compilation unit.</param>
internal sealed record AstEntry(object Node, object? Parent);

/// <summary>
/// Every node of a tree, parents first, in source order of the properties a node's constructor
/// takes (<see cref="AstPath.SyntacticChildren"/>). The helper records that are not
/// <see cref="AstNode"/>s are included, because an operator edits a type reference, a parameter and
/// a call argument as much as an expression.
/// </summary>
internal static class AstNodes {
    // Reflection over a node type is the same every time, and a campaign walks a tree per mutant.
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ChildProperties = new();

    /// <summary>Every node under and including <paramref name="root"/>.</summary>
    public static IReadOnlyList<AstEntry> Enumerate(AstNode root) {
        var entries = new List<AstEntry>();
        Visit(root, null, entries);
        return entries;
    }

    /// <summary>The source range of a node, or <see langword="null"/> for an object with none.</summary>
    public static SourceRange? RangeOf(object node) => node switch {
        AstNode n => n.Range,
        Parameter p => p.Range,
        CallArgument a => a.Range,
        TypeRef t => t.Range,
        FieldInit f => f.Range,
        MapEntry m => m.Range,
        TypeField f => f.Range,
        _ => null,
    };

    private static void Visit(object node, object? parent, List<AstEntry> entries) {
        entries.Add(new AstEntry(node, parent));
        foreach (PropertyInfo property in ChildProperties.GetOrAdd(node.GetType(), t => [.. AstPath.SyntacticChildren(t)])) {
            switch (property.GetValue(node)) {
                case { } child when AstPath.IsAstObject(child):
                    Visit(child, node, entries);
                    break;
                case IEnumerable items and not string:
                    foreach (object? item in items) {
                        if (item is not null && AstPath.IsAstObject(item)) Visit(item, node, entries);
                    }
                    break;
            }
        }
    }
}
