using Grob.Core;
using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// The <c>formatAs</c> module (D-282/D-320/D-342 — Sprint 8 Increment E): the
/// collection-to-string terminators <c>table</c>/<c>list</c>/<c>csv</c>. Compile-time
/// column derivation and the chained-form receiver rewrite live entirely in
/// <c>Grob.Compiler</c> (<c>TypeChecker.Expressions.cs</c>'s <c>ResolveFormatAsCall</c>,
/// <c>Compiler.Expressions.cs</c>'s <c>EmitFormatAsCall</c>) — by the time a script reaches
/// these natives, the compiler has already resolved and injected the ordered column-name
/// list as the second argument, so no reflection over the value happens here. Every
/// member's registered arity is fixed at 2 regardless of which source overload or call
/// form (function or chained) the user wrote. Cell values render through <see
/// cref="IPluginRegistrar.RenderValue"/> — the VM's real, registry-backed <c>ValueDisplay</c>
/// (D-336) — captured at <see cref="Register"/> time, so a plugin type's registered
/// <c>toString()</c> (<c>guid</c>, Sprint 8 Increment D) renders correctly here too, and
/// float cells stay culture-pinned end to end.
/// </summary>
public sealed class FormatAsPlugin : IGrobPlugin {
    /// <inheritdoc/>
    public string Name => "formatAs";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);
        Func<GrobValue, string> render = registrar.RenderValue;

        registrar.RegisterNative("formatAs.table", new NativeFunction("formatAs.table", 2, (args, _) =>
            GrobValue.FromString(Table(args[0].AsArray(), ColumnNames(args[1]), render))));

        registrar.RegisterNative("formatAs.list", new NativeFunction("formatAs.list", 2, (args, _) =>
            GrobValue.FromString(ListOf(args[0].AsStruct(), ColumnNames(args[1]), render))));

        registrar.RegisterNative("formatAs.csv", new NativeFunction("formatAs.csv", 2, (args, _) =>
            GrobValue.FromString(Csv(args[0].AsArray(), ColumnNames(args[1]), render))));
    }

    private static List<string> ColumnNames(GrobValue columns) =>
        [.. columns.AsArray().Elements.Select(v => v.AsString())];

    // -----------------------------------------------------------------------
    // table — header row + data rows, auto-sized widths, strings left-aligned,
    // numbers right-aligned.
    // -----------------------------------------------------------------------

    private static string Table(GrobArray items, IReadOnlyList<string> columns, Func<GrobValue, string> render) {
        var rows = new List<string[]>(items.Count);
        foreach (GrobStruct row in items.Elements.Select(static item => item.AsStruct())) {
            rows.Add([.. columns.Select(c => render(row.GetField(c)))]);
        }

        GrobStruct? firstRow = items.Count > 0 ? items[0].AsStruct() : null;
        bool[] rightAlign = [.. columns.Select(c => firstRow is not null && IsNumeric(firstRow.GetField(c)))];
        int[] widths = new int[columns.Count];
        for (int i = 0; i < columns.Count; i++) {
            widths[i] = columns[i].Length;
            foreach (string[] row in rows) widths[i] = Math.Max(widths[i], row[i].Length);
        }

        var lines = new List<string>(rows.Count + 1) { FormatRow(columns, widths, rightAlign) };
        foreach (string[] row in rows) lines.Add(FormatRow(row, widths, rightAlign));
        return string.Join("\n", lines);
    }

    private static string FormatRow(IReadOnlyList<string> cells, IReadOnlyList<int> widths, IReadOnlyList<bool> rightAlign) {
        var padded = new string[cells.Count];
        for (int i = 0; i < cells.Count; i++) {
            padded[i] = rightAlign[i] ? cells[i].PadLeft(widths[i]) : cells[i].PadRight(widths[i]);
        }
        return string.Join("  ", padded).TrimEnd();
    }

    private static bool IsNumeric(GrobValue value) => value.Kind is GrobValueKind.Int or GrobValueKind.Float;

    // -----------------------------------------------------------------------
    // list — one 'field: value' line per field, single-record detail view.
    // -----------------------------------------------------------------------

    private static string ListOf(GrobStruct item, IReadOnlyList<string> fields, Func<GrobValue, string> render) =>
        string.Join("\n", fields.Select(f => $"{f}: {render(item.GetField(f))}"));

    // -----------------------------------------------------------------------
    // csv — header row + data rows, comma-delimited, RFC 4180-style quoting.
    // -----------------------------------------------------------------------

    private static string Csv(GrobArray items, IReadOnlyList<string> columns, Func<GrobValue, string> render) {
        var lines = new List<string>(items.Count + 1) { string.Join(",", columns.Select(CsvField)) };
        foreach (GrobStruct row in items.Elements.Select(static item => item.AsStruct())) {
            lines.Add(string.Join(",", columns.Select(c => CsvField(render(row.GetField(c))))));
        }
        return string.Join("\n", lines);
    }

    private static string CsvField(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
