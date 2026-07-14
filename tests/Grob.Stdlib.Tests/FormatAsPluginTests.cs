using Grob.Core;
using Grob.Vm;
using Xunit;

using static Grob.Stdlib.Tests.ChunkBuilders;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Sprint 8 Increment E: <see cref="FormatAsPlugin"/> registers <c>table</c>/<c>list</c>/
/// <c>csv</c>, each with a fixed runtime arity of 2 (items/item, columns/fields) — the
/// compiler has already resolved and injected the column list by the time these natives
/// run, so no reflection over the value happens here. Chunks are hand-constructed;
/// this project has no dependency on <c>Grob.Compiler</c>.
/// </summary>
public sealed class FormatAsPluginTests {
    private static VirtualMachine NewRegisteredVm() {
        var vm = new VirtualMachine(new StringWriter());
        new FormatAsPlugin().Register(vm);
        return vm;
    }

    private static GrobValue Row(string typeName, params (string Name, GrobValue Value)[] fields) =>
        GrobValue.FromStruct(new GrobStruct(
            typeName, fields.Select(f => new KeyValuePair<string, GrobValue>(f.Name, f.Value))));

    private static GrobValue Arr(params GrobValue[] elements) => GrobValue.FromArray(new GrobArray(elements));

    private static GrobValue Cols(params string[] names) => Arr([.. names.Select(GrobValue.FromString)]);

    [Fact]
    public void Name_IsFormatAs() {
        Assert.Equal("formatAs", new FormatAsPlugin().Name);
    }

    // -----------------------------------------------------------------------
    // table — auto-sized widths, strings left-aligned, numbers right-aligned.
    // -----------------------------------------------------------------------

    [Fact]
    public void Table_StringAndIntColumns_AlignsLeftAndRightWithAutoWidth() {
        var vm = NewRegisteredVm();
        GrobValue items = Arr(
            Row("Item", ("id", GrobValue.FromInt(1)), ("tag", GrobValue.FromString("x"))),
            Row("Item", ("id", GrobValue.FromInt(22)), ("tag", GrobValue.FromString("yy"))));
        GrobValue columns = Cols("id", "tag");

        vm.Run(BuildCallChunk("formatAs.table", items, columns));

        Assert.Equal("id  tag\n 1  x\n22  yy", vm.Stack.Peek().AsString());
    }

    [Fact]
    public void Table_EmptyArray_RendersHeaderOnly() {
        var vm = NewRegisteredVm();
        GrobValue items = Arr();
        GrobValue columns = Cols("id", "tag");

        vm.Run(BuildCallChunk("formatAs.table", items, columns));

        Assert.Equal("id  tag", vm.Stack.Peek().AsString());
    }

    [Fact]
    public void Table_FloatCell_RendersRoundTrippableWithDecimalPoint() {
        var vm = NewRegisteredVm();
        GrobValue items = Arr(Row("Item", ("price", GrobValue.FromFloat(12.0))));
        GrobValue columns = Cols("price");

        vm.Run(BuildCallChunk("formatAs.table", items, columns));

        Assert.Equal("price\n 12.0", vm.Stack.Peek().AsString());
    }

    [Fact]
    public void Table_ColumnsSubsetAndReordered_RendersOnlyThoseColumnsInGivenOrder() {
        var vm = NewRegisteredVm();
        GrobValue items = Arr(Row("Item",
            ("name", GrobValue.FromString("Widget")),
            ("price", GrobValue.FromFloat(9.5)),
            ("qty", GrobValue.FromInt(3))));
        GrobValue columns = Cols("price", "name");

        vm.Run(BuildCallChunk("formatAs.table", items, columns));

        Assert.Equal("price  name\n  9.5  Widget", vm.Stack.Peek().AsString());
    }

    // -----------------------------------------------------------------------
    // list — one 'field: value' line per field.
    // -----------------------------------------------------------------------

    [Fact]
    public void List_SingleRecord_RendersOneFieldPerLine() {
        var vm = NewRegisteredVm();
        GrobValue item = Row("Item", ("name", GrobValue.FromString("Widget")), ("price", GrobValue.FromFloat(9.5)));
        GrobValue fields = Cols("name", "price");

        vm.Run(BuildCallChunk("formatAs.list", item, fields));

        Assert.Equal("name: Widget\nprice: 9.5", vm.Stack.Peek().AsString());
    }

    // -----------------------------------------------------------------------
    // csv — header row, comma-delimited, no padding, RFC 4180-style quoting.
    // -----------------------------------------------------------------------

    [Fact]
    public void Csv_HeaderAndRows_AreCommaDelimitedWithNoPadding() {
        var vm = NewRegisteredVm();
        GrobValue items = Arr(
            Row("Item", ("name", GrobValue.FromString("Widget")), ("price", GrobValue.FromFloat(9.5))),
            Row("Item", ("name", GrobValue.FromString("Gadget")), ("price", GrobValue.FromFloat(12.0))));
        GrobValue columns = Cols("name", "price");

        vm.Run(BuildCallChunk("formatAs.csv", items, columns));

        Assert.Equal("name,price\nWidget,9.5\nGadget,12.0", vm.Stack.Peek().AsString());
    }

    [Fact]
    public void Csv_FieldContainingComma_IsQuoted() {
        var vm = NewRegisteredVm();
        GrobValue items = Arr(Row("Item", ("note", GrobValue.FromString("a, b"))));
        GrobValue columns = Cols("note");

        vm.Run(BuildCallChunk("formatAs.csv", items, columns));

        Assert.Equal("note\n\"a, b\"", vm.Stack.Peek().AsString());
    }

    [Fact]
    public void Csv_FieldContainingQuote_IsQuotedAndEscaped() {
        var vm = NewRegisteredVm();
        GrobValue items = Arr(Row("Item", ("note", GrobValue.FromString("say \"hi\""))));
        GrobValue columns = Cols("note");

        vm.Run(BuildCallChunk("formatAs.csv", items, columns));

        Assert.Equal("note\n\"say \"\"hi\"\"\"", vm.Stack.Peek().AsString());
    }

    // -----------------------------------------------------------------------
    // Cell rendering goes through the injected RenderValue — a registered
    // toString() (guid, Sprint 8 Increment D) renders its canonical form, not its
    // hidden fields.
    // -----------------------------------------------------------------------

    [Fact]
    public void Table_CellWithRegisteredToString_RendersCanonicalFormNotFields() {
        var vm = NewRegisteredVm();
        vm.RegisterToString("guid", v => "canonical-" + v.AsStruct().GetField("__value").AsString());
        GrobValue g = GrobValue.FromStruct(new GrobStruct(
            "guid", [new KeyValuePair<string, GrobValue>("__value", GrobValue.FromString("abc"))]));
        GrobValue items = Arr(Row("Item", ("id", g)));
        GrobValue columns = Cols("id");

        vm.Run(BuildCallChunk("formatAs.table", items, columns));

        Assert.Equal("id\ncanonical-abc", vm.Stack.Peek().AsString());
    }
}
