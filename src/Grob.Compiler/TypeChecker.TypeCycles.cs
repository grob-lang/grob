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
    // Cycle classification:
    //   - Back-edge target found in the path → slice the path from the first
    //     occurrence of the target to determine the actual cycle.
    //   - Slice length == 1 AND slice[0].TypeName == target → E0302 (trivial
    //     self-reference: the field's own type is the cycle).
    //   - Otherwise → E0301 (multi-type cycle; full cycle path reported).
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
            // Only required (no default) non-nullable Struct fields participate.
            if (!field.IsRequired || field.Kind != GrobType.Struct || field.NamedTypeName is null)
                continue;

            // Target not registered means it had an E1001 emitted during field
            // resolution; skip to avoid a cascade.
            if (!colors.TryGetValue(field.NamedTypeName, out TypeCycleColor targetColor))
                continue;

            path.Add((typeName, field.Name, field.Range));

            if (targetColor == TypeCycleColor.Unvisited) {
                WalkTypeCycle(field.NamedTypeName, colors, path);
            } else if (targetColor == TypeCycleColor.Visiting) {
                EmitCycleError(field.NamedTypeName, path);
            }
            // Visited: already fully explored — no cycle through this edge.

            path.RemoveAt(path.Count - 1);
        }

        colors[typeName] = TypeCycleColor.Visited; // NOSONAR — intentional Visiting→Visited DFS completion; the first write is read by recursive sub-calls
    }

    /// <summary>
    /// Emits E0302 (trivial self-reference) or E0301 (multi-type cycle) after a
    /// back-edge is detected in <see cref="WalkTypeCycle"/>. The actual cycle is
    /// determined by slicing <paramref name="path"/> from the first entry whose
    /// <c>TypeName</c> equals <paramref name="target"/> — so a self-reference
    /// discovered through a longer prefix (e.g. A → B → B) is correctly reported
    /// as E0302 on B's own field, not E0301 on A's field.
    /// </summary>
    private void EmitCycleError(
        string target,
        List<(string TypeName, string FieldName, SourceRange FieldRange)> path) {

        int cycleStart = path.FindIndex(e => e.TypeName == target);
        // cycleStart is always ≥ 0: target is Visiting, so it is on the path.
        List<(string TypeName, string FieldName, SourceRange FieldRange)> cycle =
            path.GetRange(cycleStart, path.Count - cycleStart);

        if (cycle.Count == 1) {
            // Trivial self-reference: type T { field: T } → E0302.
            EmitError(ErrorCatalog.E0302,
                $"Type '{target}' has a required field '{cycle[0].FieldName}' of its own type, "
              + $"which would require an infinitely large value. "
              + $"Use '{target}?' or '{target}[]' to break the recursion.",
                cycle[0].FieldRange);
        } else {
            // Multi-type cycle → E0301. Report the full cycle path.
            string cyclePath = string.Join(" → ", cycle.Select(e => e.TypeName).Append(target));
            EmitError(ErrorCatalog.E0301,
                $"Type cycle with no terminating field: {cyclePath}. "
              + "Make one of the fields nullable (T?) or a collection (T[]) to break the cycle.",
                cycle[0].FieldRange);
        }
    }
}
