using Grob.Core;

namespace Grob.Compiler;

public sealed partial class TypeChecker {
    // -----------------------------------------------------------------------
    // Phase 2.5 — §17.1 required-non-nullable field-cycle detection (D-287).
    //
    // Runs after all TypeDecl pass-2 visits, so every type's fields are resolved
    // and registered in _userTypeRegistry. The DFS mirrors the value-binding cycle
    // detection in TypeChecker.ValueResolution.cs.
    //
    // What participates: required (no default) fields whose resolved kind is
    // GrobType.Struct — a non-nullable named user-defined type.
    //
    // What terminates (does not participate):
    //   - GrobType.NullableStruct  (T?  — nil terminates)
    //   - GrobType.Array           (T[] — empty array terminates)
    //   - GrobType.NullableArray   (T[]? — nil or empty terminates)
    //   - GrobType.Map             (map<K,V> — empty map terminates)
    //   - Any built-in or function type
    //
    // Back-edge detection:
    //   - target == startType AND path has exactly one entry → E0302 (trivial self-ref)
    //   - otherwise → E0301 (multi-type cycle, full path reported)
    // -----------------------------------------------------------------------

    private enum TypeCycleColor { Unvisited, Visiting, Visited }

    private void DetectTypeCycles() {
        Dictionary<string, TypeCycleColor> colors = new(StringComparer.Ordinal);
        foreach (UserTypeInfo t in _userTypeRegistry.AllTypes)
            colors[t.Name] = TypeCycleColor.Unvisited;

        // Path stack carries (typeName, fieldName, fieldRange) for diagnostic messages.
        List<(string TypeName, string FieldName, SourceRange FieldRange)> path = [];

        foreach (UserTypeInfo t in _userTypeRegistry.AllTypes) {
            if (colors[t.Name] == TypeCycleColor.Unvisited)
                WalkTypeCycle(t.Name, colors, path);
        }
    }

    private void WalkTypeCycle(
        string typeName,
        Dictionary<string, TypeCycleColor> colors,
        List<(string TypeName, string FieldName, SourceRange FieldRange)> path) {

        colors[typeName] = TypeCycleColor.Visiting;
        UserTypeInfo type = _userTypeRegistry.TryGet(typeName)!;

        foreach (ResolvedFieldInfo field in type.Fields) {
            // Only required (no default) non-nullable struct fields participate.
            if (!field.IsRequired) continue;
            if (field.Kind != GrobType.Struct) continue;
            if (field.NamedTypeName is null) continue;

            string target = field.NamedTypeName;

            // Target not registered means it had an E1001 emitted during field
            // resolution; skip to avoid a cascade.
            if (!colors.ContainsKey(target)) continue;

            path.Add((typeName, field.Name, field.Range));

            if (colors[target] == TypeCycleColor.Unvisited) {
                WalkTypeCycle(target, colors, path);
            } else if (colors[target] == TypeCycleColor.Visiting) {
                // Back-edge: cycle detected.
                if (target == typeName && path.Count == 1) {
                    // Trivial self-reference: type A { a: A } → E0302
                    EmitError(ErrorCatalog.E0302,
                        $"Type '{typeName}' has a required field '{field.Name}' of its own type, "
                      + $"which would require an infinitely large value. "
                      + $"Use '{typeName}?' or '{typeName}[]' to break the recursion.",
                        field.Range);
                } else {
                    // Multi-type cycle → E0301.  Report the full path.
                    string cyclePath = string.Join(" → ",
                        path.Select(p => p.TypeName).Append(target));
                    EmitError(ErrorCatalog.E0301,
                        $"Type cycle with no terminating field: {cyclePath}. "
                      + "Make one of the fields nullable (T?) or a collection (T[]) to break the cycle.",
                        path[0].FieldRange);
                }
            }
            // Visited: node already fully explored — no cycle through this edge.

            path.RemoveAt(path.Count - 1);
        }

        colors[typeName] = TypeCycleColor.Visited;
    }
}
