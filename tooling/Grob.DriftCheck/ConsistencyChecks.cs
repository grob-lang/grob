using System.Text.RegularExpressions;
using Grob.Core;

namespace Grob.DriftCheck;

/// <summary>
/// A single ADR cross-reference found in the design corpus.
/// </summary>
/// <param name="Document">The file the reference was found in.</param>
/// <param name="Line">The 1-based line number of the reference.</param>
/// <param name="Code">The four-digit ADR number referenced, e.g. <c>0012</c>.</param>
public sealed record AdrReference(string Document, int Line, string Code);

/// <summary>
/// The facts the lockstep check needs from the decisions log, parsed once so the
/// pure check is unit-testable with planted inputs.
/// </summary>
/// <param name="IndexCodes">Numeric <c>D-###</c> codes from the summary-index rows, in order, including any duplicates.</param>
/// <param name="EntryCodes">Numeric <c>D-###</c> codes from the full <c>### D-### —</c> entries.</param>
/// <param name="AllEntryCodes">Every full-entry code including the post-MVP <c>D-PM-###</c> series, for supersession resolution.</param>
/// <param name="SupersessionTargets">Every <c>Supersedes:</c> / <c>Superseded by:</c> target code.</param>
public sealed record DecisionsLogFacts(
    IReadOnlyList<string> IndexCodes,
    IReadOnlyList<string> EntryCodes,
    IReadOnlyList<string> AllEntryCodes,
    IReadOnlyList<string> SupersessionTargets);

/// <summary>
/// An atom extracted from the §3.4 TokenKind listing, with the section for
/// diagnostics.
/// </summary>
/// <param name="Section">The §3.4 sub-section the atom came from (Keywords, Operators, …).</param>
/// <param name="Text">The atom as written in the spec (a keyword word, an operator glyph or a named token).</param>
public sealed record SpecAtom(string Section, string Text);

/// <summary>
/// The corpus consistency checks (D-316). Each public check is a pure comparison
/// of two live facts and returns a <see cref="CheckResult"/>; the parsers that
/// feed them are thin, defensive and throw <see cref="AnchorNotFoundException"/>
/// rather than returning empty when an anchor is missing. The console wrapper
/// and the xUnit gate both drive this one implementation — there is no second
/// copy of the logic.
/// </summary>
public static partial class ConsistencyChecks {
    // -------------------------------------------------------------------------
    // 4.1.1 — Error-code count agreement
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asserts the summary-index code count, the canonical footer total and
    /// <see cref="ErrorCatalog"/>'s descriptor count are all equal. Closes the
    /// 86 / 94 / 98 / 99 stale-count class permanently.
    /// </summary>
    /// <param name="summaryIndexCount">Codes counted in the registry summary index.</param>
    /// <param name="catalogCount">Descriptor count from <see cref="ErrorCatalog.All"/>.</param>
    /// <param name="footerTotal">The number stated in the registry's canonical total line.</param>
    /// <returns>A passing result when all three agree, otherwise the disagreements.</returns>
    public static CheckResult CompareErrorCodeCount(int summaryIndexCount, int catalogCount, int footerTotal) {
        var failures = new List<string>();
        if (summaryIndexCount != catalogCount) {
            failures.Add(
                $"grob-error-codes.md summary index has {summaryIndexCount} codes but " +
                $"ErrorCatalog.All has {catalogCount}.");
        }
        if (footerTotal != catalogCount) {
            failures.Add(
                $"grob-error-codes.md canonical total states {footerTotal} codes but " +
                $"ErrorCatalog.All has {catalogCount}.");
        }
        if (footerTotal != summaryIndexCount) {
            failures.Add(
                $"grob-error-codes.md canonical total states {footerTotal} codes but the " +
                $"summary index lists {summaryIndexCount}.");
        }
        return failures.Count == 0
            ? CheckResult.Pass("error-code count agreement")
            : CheckResult.Fail("error-code count agreement", failures);
    }

    // -------------------------------------------------------------------------
    // 4.1.2 — Decisions-log lockstep
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asserts the decisions log holds lockstep: no duplicate index code, an exact
    /// bijection between numeric index rows and full entries, and every
    /// supersession target resolving to a defined entry.
    /// </summary>
    /// <param name="facts">The parsed decisions-log facts.</param>
    /// <returns>A passing result when lockstep holds, otherwise the violations.</returns>
    public static CheckResult CheckDecisionsLockstep(DecisionsLogFacts facts) {
        var failures = new List<string>();

        var duplicates = facts.IndexCodes
            .GroupBy(c => c)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
        if (duplicates.Count > 0) {
            failures.Add(
                "grob-decisions-log.md summary index has duplicate D-### rows: " +
                string.Join(", ", duplicates));
        }

        var indexSet = facts.IndexCodes.ToHashSet();
        var entrySet = facts.EntryCodes.ToHashSet();

        var indexWithoutEntry = indexSet.Except(entrySet).OrderBy(c => c, StringComparer.Ordinal).ToList();
        if (indexWithoutEntry.Count > 0) {
            failures.Add(
                "grob-decisions-log.md index rows with no matching '### D-### —' entry: " +
                string.Join(", ", indexWithoutEntry));
        }

        var entryWithoutIndex = entrySet.Except(indexSet).OrderBy(c => c, StringComparer.Ordinal).ToList();
        if (entryWithoutIndex.Count > 0) {
            failures.Add(
                "grob-decisions-log.md full entries with no summary-index row: " +
                string.Join(", ", entryWithoutIndex));
        }

        var defined = facts.AllEntryCodes.ToHashSet();
        var danglingTargets = facts.SupersessionTargets
            .Where(t => !defined.Contains(t))
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
        if (danglingTargets.Count > 0) {
            failures.Add(
                "grob-decisions-log.md Supersedes/Superseded-by targets with no matching entry: " +
                string.Join(", ", danglingTargets));
        }

        return failures.Count == 0
            ? CheckResult.Pass("decisions-log lockstep")
            : CheckResult.Fail("decisions-log lockstep", failures);
    }

    // -------------------------------------------------------------------------
    // 4.1.3 — ADR reference integrity
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asserts every ADR reference in the design corpus resolves to a published
    /// ADR file. Closes the <c>ADR-0007 → 0012</c> / <c>ADR-0008 → 0013</c> class.
    /// </summary>
    /// <param name="references">Every ADR reference found across the design docs.</param>
    /// <param name="available">The set of four-digit ADR numbers present under docs/wiki/ADR.</param>
    /// <returns>A passing result when every reference resolves, otherwise the dangling ones.</returns>
    public static CheckResult CheckAdrReferences(
        IReadOnlyList<AdrReference> references,
        IReadOnlySet<string> available) {
        var dangling = references
            .Where(r => !available.Contains(r.Code))
            .Select(r => $"ADR-{r.Code} referenced in {r.Document}:{r.Line} has no file under docs/wiki/ADR/")
            .Distinct()
            .OrderBy(m => m, StringComparer.Ordinal)
            .ToList();

        return dangling.Count == 0
            ? CheckResult.Pass("ADR reference integrity")
            : CheckResult.Fail("ADR reference integrity", dangling);
    }

    // -------------------------------------------------------------------------
    // 4.1.4 — OpCode and TokenKind completeness
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asserts every name the spec declares complete exists in the corresponding
    /// enum. This is the "spec says X exists — does the code have X" check.
    /// </summary>
    /// <param name="label">The enum label for messages (e.g. "OpCode").</param>
    /// <param name="declared">The names the spec lists as complete.</param>
    /// <param name="actual">The names actually present in the enum.</param>
    /// <returns>A passing result when every declared name is present, otherwise the missing ones.</returns>
    public static CheckResult CheckEnumCompleteness(
        string label,
        IReadOnlyList<string> declared,
        IReadOnlySet<string> actual) {
        var missing = declared
            .Where(name => !actual.Contains(name))
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => $"{label} '{name}' is declared in the spec but absent from the {label} enum")
            .ToList();

        return missing.Count == 0
            ? CheckResult.Pass($"{label} completeness")
            : CheckResult.Fail($"{label} completeness", missing);
    }

    /// <summary>
    /// Canonical mapping from each §3.4 atom (keyword word, operator/punctuation
    /// glyph, named literal/structure token, or the decorator glyph) to its
    /// <see cref="TokenKind"/> member name. The completeness check asserts both
    /// that every spec atom is covered here and that every mapped name exists in
    /// the enum, so a new spec glyph or a removed enum member both fail loudly.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> SpecTokenAtomMap =
        new Dictionary<string, string>(StringComparer.Ordinal) {
            // Keywords (§3.4)
            ["fn"] = "Fn",
            ["if"] = "If",
            ["else"] = "Else",
            ["while"] = "While",
            ["for"] = "For",
            ["in"] = "In",
            ["return"] = "Return",
            ["const"] = "Const",
            ["readonly"] = "Readonly",
            ["type"] = "Type",
            ["param"] = "Param",
            ["import"] = "Import",
            ["as"] = "As",
            ["try"] = "Try",
            ["catch"] = "Catch",
            ["finally"] = "Finally",
            ["throw"] = "Throw",
            // 'select' is a reserved identifier (D-320), not a keyword — it has no
            // TokenKind member and §3.4 lists it under "Reserved IDs", so it is not
            // a keyword atom here.
            ["case"] = "Case",
            ["default"] = "Default",
            ["break"] = "Break",
            ["continue"] = "Continue",
            ["true"] = "True",
            ["false"] = "False",
            ["nil"] = "Nil",
            ["step"] = "Step",
            ["switch"] = "Switch",
            // Operators (§3.4)
            ["+"] = "Plus",
            ["-"] = "Minus",
            ["*"] = "Star",
            ["/"] = "Slash",
            ["%"] = "Percent",
            ["="] = "Assign",
            [":="] = "ColonAssign",
            ["=="] = "EqualEqual",
            ["!="] = "BangEqual",
            ["<"] = "Less",
            [">"] = "Greater",
            ["<="] = "LessEqual",
            [">="] = "GreaterEqual",
            ["!"] = "Bang",
            ["&&"] = "AmpAmp",
            ["||"] = "PipePipe",
            ["?"] = "Question",
            [":"] = "Colon",
            ["??"] = "QuestionQuestion",
            ["?."] = "QuestionDot",
            ["+="] = "PlusAssign",
            ["-="] = "MinusAssign",
            ["*="] = "StarAssign",
            ["/="] = "SlashAssign",
            ["%="] = "PercentAssign",
            ["++"] = "PlusPlus",
            ["--"] = "MinusMinus",
            [".."] = "DotDot",
            ["=>"] = "Arrow",
            // Punctuation (§3.4)
            ["("] = "LeftParen",
            [")"] = "RightParen",
            ["{"] = "LeftBrace",
            ["}"] = "RightBrace",
            ["["] = "LeftBracket",
            ["]"] = "RightBracket",
            [","] = "Comma",
            ["."] = "Dot",
            ["#{"] = "HashBrace",
            ["///"] = "DocComment",
            // Literals (§3.4) — already enum-identifier form
            ["IntLiteral"] = "IntLiteral",
            ["FloatLiteral"] = "FloatLiteral",
            ["StringStart"] = "StringStart",
            ["StringPart"] = "StringPart",
            ["StringEnd"] = "StringEnd",
            ["InterpStart"] = "InterpStart",
            ["InterpEnd"] = "InterpEnd",
            ["RawStringLiteral"] = "RawStringLiteral",
            ["RawStringBlockLiteral"] = "RawStringBlockLiteral",
            ["RegexLiteral"] = "RegexLiteral",
            ["Identifier"] = "Identifier",
            // Structure (§3.4)
            ["Newline"] = "Newline",
            ["EOF"] = "Eof",
            ["Error"] = "Error",
            // Decorators (§3.4)
            ["@"] = "At",
        };

    /// <summary>
    /// Asserts every §3.4 atom maps to a known <see cref="TokenKind"/> and that the
    /// mapped member exists in the enum. Uncovered spec atoms and missing enum
    /// members both fail loudly.
    /// </summary>
    /// <param name="atoms">The atoms parsed from the §3.4 listing.</param>
    /// <param name="actual">The names actually present in <see cref="TokenKind"/>.</param>
    /// <returns>A passing result when every atom resolves, otherwise the failures.</returns>
    public static CheckResult CheckTokenKindCompleteness(
        IReadOnlyList<SpecAtom> atoms,
        IReadOnlySet<string> actual) {
        var failures = new List<string>();
        foreach (var atom in atoms) {
            if (!SpecTokenAtomMap.TryGetValue(atom.Text, out var member)) {
                failures.Add(
                    $"§3.4 lists token '{atom.Text}' ({atom.Section}) with no mapping in the gate — " +
                    "the spec gained a token the gate does not know about.");
            } else if (!actual.Contains(member)) {
                failures.Add(
                    $"§3.4 lists '{atom.Text}' ({atom.Section}) → TokenKind.{member}, " +
                    "which is absent from the TokenKind enum.");
            }
        }

        return failures.Count == 0
            ? CheckResult.Pass("TokenKind completeness")
            : CheckResult.Fail("TokenKind completeness", failures);
    }

    // -------------------------------------------------------------------------
    // 4.1.6 — native method names avoid hard keywords (D-320)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asserts that no registered native method name is a hard keyword. A hard
    /// keyword lexes to a keyword token, so the member-access parser — which expects
    /// an identifier after <c>.</c> — cannot parse <c>receiver.method(...)</c> for a
    /// method of that name. Reserved identifiers (<c>formatAs</c>, <c>select</c>) are
    /// deliberately permitted as method names; the check is against the hard-keyword
    /// set, not the reserved-identifier set (D-320). This is the durable guard that
    /// turns the next such collision into a build failure instead of an unparseable
    /// call.
    /// </summary>
    /// <param name="nativeMethodNames">The registered dotted method names.</param>
    /// <param name="hardKeywordLexemes">The hard-keyword lexemes (spec §3.4).</param>
    /// <returns>A passing result when the two sets are disjoint, otherwise a failure.</returns>
    public static CheckResult CheckNativeMethodsAvoidKeywords(
        IReadOnlySet<string> nativeMethodNames,
        IReadOnlySet<string> hardKeywordLexemes) {
        var collisions = nativeMethodNames
            .Where(hardKeywordLexemes.Contains)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name =>
                $"native method '{name}' is a hard keyword — '.{name}(...)' cannot parse " +
                "after '.'. Make it a reserved identifier or rename the method (D-320).")
            .ToList();

        return collisions.Count == 0
            ? CheckResult.Pass("native-method vs hard-keyword collision")
            : CheckResult.Fail("native-method vs hard-keyword collision", collisions);
    }

    // -------------------------------------------------------------------------
    // 4.1.5 — ErrorCatalog agreement reference (the D-308 guard is discoverable)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asserts D-308's catalog↔registry agreement test is present and recognisable,
    /// so this suite is the single index of every mechanical agreement check in the
    /// build. Does not re-run D-308's assertions.
    /// </summary>
    /// <param name="repoRoot">The repository root to search under.</param>
    /// <returns>A passing result when the guard is found, otherwise a failure.</returns>
    public static CheckResult CheckErrorCatalogGuardPresent(string repoRoot) {
        const string name = "ErrorCatalog agreement guard (D-308)";
        var path = Path.Join(
            repoRoot, "tests", "Grob.Core.Tests", "ErrorCatalogAgreementTests.cs");

        if (!File.Exists(path)) {
            return CheckResult.Fail(name, [
                $"The D-308 catalog↔registry guard was not found at {path}. " +
                "It is the load-bearing agreement test the consistency suite indexes."]);
        }

        var text = File.ReadAllText(path);
        var guards = text.Contains("ErrorCatalog", StringComparison.Ordinal)
                     && text.Contains("grob-error-codes.md", StringComparison.Ordinal);
        return guards
            ? CheckResult.Pass(name)
            : CheckResult.Fail(name, [
                $"{path} exists but no longer references both ErrorCatalog and " +
                "grob-error-codes.md — it may no longer be the catalog↔registry guard."]);
    }

    // -------------------------------------------------------------------------
    // Live-enum / catalog adapters (the "actual" side)
    // -------------------------------------------------------------------------

    /// <summary>The number of descriptors in <see cref="ErrorCatalog.All"/>.</summary>
    /// <returns>The live catalog count.</returns>
    public static int ActualErrorCatalogCount() => ErrorCatalog.All.Count;

    /// <summary>The names of every member of <see cref="OpCode"/>.</summary>
    /// <returns>The live opcode-name set.</returns>
    public static IReadOnlySet<string> ActualOpCodeNames() => Enum.GetNames<OpCode>().ToHashSet();

    /// <summary>The names of every member of <see cref="TokenKind"/>.</summary>
    /// <returns>The live token-kind-name set.</returns>
    public static IReadOnlySet<string> ActualTokenKindNames() => Enum.GetNames<TokenKind>().ToHashSet();

    // -------------------------------------------------------------------------
    // Source-generated regexes (the "stated" side)
    // -------------------------------------------------------------------------

    // Every pattern below is linear (no nested quantifiers over overlapping
    // classes), but a match timeout is set as a defence-in-depth ReDoS backstop
    // even though the inputs are trusted in-repo documents.
    private const int RegexTimeoutMs = 2_000;

    [GeneratedRegex(@"^\|\s*(E\d{4})\s*\|", RegexOptions.None, RegexTimeoutMs)]
    private static partial Regex SummaryRowRegex();

    [GeneratedRegex(@"\*\*Total:\s*(\d+)\s*codes", RegexOptions.None, RegexTimeoutMs)]
    private static partial Regex FooterTotalRegex();

    [GeneratedRegex(@"^\|\s*(D-\d+)\s*\|", RegexOptions.None, RegexTimeoutMs)]
    private static partial Regex IndexCodeRegex();

    [GeneratedRegex(@"^###\s+(D-(?:PM-)?\d+)\b", RegexOptions.None, RegexTimeoutMs)]
    private static partial Regex FullEntryRegex();

    [GeneratedRegex(@"(?:Supersedes|Superseded by):", RegexOptions.None, RegexTimeoutMs)]
    private static partial Regex SupersessionHeaderRegex();

    [GeneratedRegex(@"D-(?:PM-)?\d+", RegexOptions.None, RegexTimeoutMs)]
    private static partial Regex DecisionCodeRegex();

    [GeneratedRegex(@"ADR-(\d{4})", RegexOptions.None, RegexTimeoutMs)]
    private static partial Regex AdrReferenceRegex();

    [GeneratedRegex(@"^(\d{4})-", RegexOptions.None, RegexTimeoutMs)]
    private static partial Regex AdrFileRegex();

    [GeneratedRegex(@"^\s*([A-Za-z_]\w*)\s*,", RegexOptions.None, RegexTimeoutMs)]
    private static partial Regex EnumMemberRegex();

    // A native-method arm in ArrayNatives.GetMethod: "name" => new NativeFunction(.
    [GeneratedRegex("\"([A-Za-z_]\\w*)\"\\s*=>\\s*new NativeFunction\\(", RegexOptions.None, RegexTimeoutMs)]
    private static partial Regex NativeMethodArmRegex();

    // A §3.4 sub-section: a label at column 0, then its body up to the next label
    // or the end of the block. Singleline lets the body span continuation lines.
    [GeneratedRegex(@"^([A-Za-z][A-Za-z-]*):[ \t]*(.*?)(?=^[A-Za-z][A-Za-z-]*:|\z)",
        RegexOptions.Multiline | RegexOptions.Singleline, RegexTimeoutMs)]
    private static partial Regex TokenSectionRegex();

    // -------------------------------------------------------------------------
    // Defensive parsers (the "stated" side) — throw on missing anchors
    // -------------------------------------------------------------------------

    /// <summary>
    /// Counts the distinct codes in the registry summary index.
    /// </summary>
    /// <param name="errorCodesPath">Path to grob-error-codes.md.</param>
    /// <returns>The number of distinct codes in the summary-index section.</returns>
    /// <exception cref="AnchorNotFoundException">The summary index could not be located.</exception>
    public static int ParseSummaryIndexCount(string errorCodesPath) {
        var codes = SectionLines(errorCodesPath, "## Summary Index", "## ")
            .Select(line => SummaryRowRegex().Match(line))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        if (codes.Count == 0) {
            throw new AnchorNotFoundException(
                Path.GetFileName(errorCodesPath), "## Summary Index (with | Exxxx | rows)");
        }
        return codes.Count;
    }

    /// <summary>
    /// Reads the canonical "**Total: N codes**" line from the registry footer.
    /// </summary>
    /// <param name="errorCodesPath">Path to grob-error-codes.md.</param>
    /// <returns>The stated total.</returns>
    /// <exception cref="AnchorNotFoundException">No canonical total line was found.</exception>
    public static int ParseFooterTotal(string errorCodesPath) {
        var match = File.ReadLines(errorCodesPath)
            .Select(line => FooterTotalRegex().Match(line))
            .FirstOrDefault(m => m.Success);
        if (match is not null) return int.Parse(match.Groups[1].Value);

        throw new AnchorNotFoundException(
            Path.GetFileName(errorCodesPath), "**Total: N codes …** canonical total line");
    }

    /// <summary>
    /// Parses the decisions-log facts the lockstep check needs.
    /// </summary>
    /// <param name="decisionsLogPath">Path to grob-decisions-log.md.</param>
    /// <returns>The parsed facts.</returns>
    /// <exception cref="AnchorNotFoundException">Neither index rows nor full entries were found.</exception>
    public static DecisionsLogFacts ParseDecisionsLog(string decisionsLogPath) {
        var indexCodes = new List<string>();
        var numericEntries = new List<string>();
        var allEntries = new List<string>();
        var supersession = new List<string>();

        foreach (var line in File.ReadLines(decisionsLogPath)) {
            var idx = IndexCodeRegex().Match(line);
            if (idx.Success) indexCodes.Add(idx.Groups[1].Value);

            var entry = FullEntryRegex().Match(line);
            if (entry.Success) {
                var code = entry.Groups[1].Value;
                allEntries.Add(code);
                if (!code.StartsWith("D-PM-", StringComparison.Ordinal)) numericEntries.Add(code);
            }

            // A single line can list several targets ("Superseded by: D-288, D-291"),
            // so extract every code after the header, not just the first.
            if (SupersessionHeaderRegex().IsMatch(line)) {
                foreach (Match s in DecisionCodeRegex().Matches(line)) {
                    supersession.Add(s.Value);
                }
            }
        }

        if (indexCodes.Count == 0 || numericEntries.Count == 0) {
            throw new AnchorNotFoundException(
                Path.GetFileName(decisionsLogPath),
                "| D-### | summary-index rows and ### D-### — full entries");
        }

        return new DecisionsLogFacts(indexCodes, numericEntries, allEntries, supersession);
    }

    /// <summary>
    /// Collects every ADR reference across the design markdown files.
    /// </summary>
    /// <param name="designDir">The docs/design directory.</param>
    /// <returns>Every ADR reference found, with document and line.</returns>
    /// <exception cref="AnchorNotFoundException">No ADR references were found at all.</exception>
    public static IReadOnlyList<AdrReference> ParseAdrReferences(string designDir) {
        var references = new List<AdrReference>();
        foreach (var file in Directory.EnumerateFiles(designDir, "*.md", SearchOption.TopDirectoryOnly)) {
            var name = Path.GetFileName(file);
            var lineNo = 0;
            foreach (var line in File.ReadLines(file)) {
                lineNo++;
                foreach (Match m in AdrReferenceRegex().Matches(line)) {
                    references.Add(new AdrReference(name, lineNo, m.Groups[1].Value));
                }
            }
        }

        if (references.Count == 0) {
            throw new AnchorNotFoundException(
                Path.GetFileName(designDir), "ADR-00NN references in docs/design/*.md");
        }
        return references;
    }

    /// <summary>
    /// Collects the set of ADR numbers that have a published file.
    /// </summary>
    /// <param name="adrDir">The docs/wiki/ADR directory.</param>
    /// <returns>The set of four-digit ADR numbers with a file.</returns>
    /// <exception cref="AnchorNotFoundException">The ADR directory holds no numbered files.</exception>
    public static IReadOnlySet<string> ParseAvailableAdrs(string adrDir) {
        var available = Directory.EnumerateFiles(adrDir, "*.md", SearchOption.TopDirectoryOnly)
            .Select(file => AdrFileRegex().Match(Path.GetFileName(file)))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        if (available.Count == 0) {
            throw new AnchorNotFoundException(
                Path.GetFileName(adrDir), "NNNN-*.md ADR files under docs/wiki/ADR");
        }
        return available;
    }

    /// <summary>
    /// Parses the opcode names the spec lists in the §3.3 enum block. Comment and
    /// brace lines do not match the member pattern, so no separate filtering is
    /// needed — only <c>Name,</c> lines are collected.
    /// </summary>
    /// <param name="requirementsPath">Path to grob-v1-requirements.md.</param>
    /// <returns>The opcode identifiers declared in §3.3, in order.</returns>
    /// <exception cref="AnchorNotFoundException">The §3.3 opcode block could not be located.</exception>
    public static IReadOnlyList<string> ParseSpecOpCodes(string requirementsPath) {
        var block = FencedBlock(requirementsPath, "### 3.3", "### 3.4", "### 3.3 OpCode enum csharp block");

        var names = block
            .Select(line => EnumMemberRegex().Match(line.Trim()))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .ToList();

        if (names.Count == 0) {
            throw new AnchorNotFoundException(
                Path.GetFileName(requirementsPath), "### 3.3 OpCode enum members (Name, lines)");
        }
        return names;
    }

    /// <summary>
    /// Parses the §3.4 TokenKind listing into atoms, one per keyword, glyph or
    /// named token the spec declares.
    /// </summary>
    /// <param name="requirementsPath">Path to grob-v1-requirements.md.</param>
    /// <returns>Every §3.4 atom the gate checks.</returns>
    /// <exception cref="AnchorNotFoundException">The §3.4 fenced listing could not be located.</exception>
    public static IReadOnlyList<SpecAtom> ParseSpecTokenAtoms(string requirementsPath) {
        var block = FencedBlock(requirementsPath, "### 3.4", "### 3.5", "### 3.4 TokenKind fenced listing");
        var blockText = string.Join("\n", block);

        var atoms = TokenSectionRegex()
            .Matches(blockText)
            .SelectMany(m => AtomsForSection(m.Groups[1].Value, m.Groups[2].Value))
            .ToList();

        if (atoms.Count == 0) {
            throw new AnchorNotFoundException(
                Path.GetFileName(requirementsPath), "### 3.4 token atoms (keywords/operators/…)");
        }
        return atoms;
    }

    /// <summary>
    /// Parses the registered array higher-order method names from the
    /// <c>ArrayNatives.GetMethod</c> switch — the live registry of dotted method
    /// names. Each arm has the shape <c>"name" =&gt; new NativeFunction(...)</c>.
    /// </summary>
    /// <param name="arrayNativesPath">Path to <c>src/Grob.Vm/ArrayNatives.cs</c>.</param>
    /// <returns>The registered method-name set.</returns>
    /// <exception cref="AnchorNotFoundException">No native-method arms were found.</exception>
    public static IReadOnlySet<string> ParseArrayNativeMethodNames(string arrayNativesPath) {
        var text = File.ReadAllText(arrayNativesPath);
        var names = NativeMethodArmRegex()
            .Matches(text)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        if (names.Count == 0) {
            throw new AnchorNotFoundException(
                Path.GetFileName(arrayNativesPath),
                "ArrayNatives.GetMethod switch arms (\"name\" => new NativeFunction(...))");
        }
        return names;
    }

    // -------------------------------------------------------------------------
    // Parsing helpers
    // -------------------------------------------------------------------------

    private static readonly char[] _commaSeparators = [',', ' ', '\t', '\n', '\r'];
    private static readonly char[] _whitespaceSeparators = [' ', '\t', '\n', '\r'];
    private static readonly HashSet<string> _whitespaceSplitSections =
        new(StringComparer.Ordinal) { "Operators", "Punctuation" };
    private static readonly HashSet<string> _commaSplitSections =
        new(StringComparer.Ordinal) { "Keywords", "Literals", "Structure" };

    /// <summary>
    /// Yields the lines of a markdown section, from the line after
    /// <paramref name="startHeading"/> up to (but excluding) the next line starting
    /// with <paramref name="endHeading"/>.
    /// </summary>
    private static IEnumerable<string> SectionLines(string path, string startHeading, string endHeading) {
        var inSection = false;
        foreach (var line in File.ReadLines(path)) {
            if (!inSection) {
                inSection = line.StartsWith(startHeading, StringComparison.Ordinal);
                continue;
            }
            if (line.StartsWith(endHeading, StringComparison.Ordinal)) yield break;
            yield return line;
        }
    }

    /// <summary>
    /// Returns the lines inside the first fenced code block within the section
    /// bounded by <paramref name="startHeading"/> and <paramref name="endHeading"/>.
    /// </summary>
    /// <exception cref="AnchorNotFoundException">No non-empty fenced block was found.</exception>
    private static List<string> FencedBlock(
        string path, string startHeading, string endHeading, string anchor) {
        var block = new List<string>();
        var inBlock = false;
        foreach (var line in SectionLines(path, startHeading, endHeading)) {
            var fence = line.TrimStart().StartsWith("```", StringComparison.Ordinal);
            if (!inBlock) {
                if (fence) inBlock = true;
                continue;
            }
            if (fence) break;
            block.Add(line);
        }

        if (block.Count == 0) {
            throw new AnchorNotFoundException(Path.GetFileName(path), anchor);
        }
        return block;
    }

    /// <summary>
    /// Splits one §3.4 sub-section's body into atoms. Built-ins are excluded (the
    /// spec resolves print/exit/input as identifiers, not token kinds); the
    /// decorator section contributes only the <c>@</c> glyph.
    /// </summary>
    private static IEnumerable<SpecAtom> AtomsForSection(string label, string body) {
        if (label is "Builtins" or "Built-ins") return [];
        if (label == "Decorators") return [new SpecAtom(label, "@")];

        if (_whitespaceSplitSections.Contains(label)) return Atoms(label, body, _whitespaceSeparators);
        if (_commaSplitSections.Contains(label)) return Atoms(label, body, _commaSeparators);
        return [];
    }

    private static IEnumerable<SpecAtom> Atoms(string label, string body, char[] separators)
        => body
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => new SpecAtom(label, token));

    // -------------------------------------------------------------------------
    // Orchestration — wire parsers to live facts and run every check
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs every consistency check against the live corpus and enums, resolving
    /// all paths from the repository root.
    /// </summary>
    /// <returns>One <see cref="CheckResult"/> per check, in a stable order.</returns>
    public static IReadOnlyList<CheckResult> RunAll() => [
        CompareErrorCodeCount(
            ParseSummaryIndexCount(RepoPaths.ErrorCodes),
            ActualErrorCatalogCount(),
            ParseFooterTotal(RepoPaths.ErrorCodes)),
        CheckDecisionsLockstep(ParseDecisionsLog(RepoPaths.DecisionsLog)),
        CheckAdrReferences(
            ParseAdrReferences(RepoPaths.DesignDir),
            ParseAvailableAdrs(RepoPaths.AdrDir)),
        CheckEnumCompleteness(
            "OpCode", ParseSpecOpCodes(RepoPaths.Requirements), ActualOpCodeNames()),
        CheckTokenKindCompleteness(
            ParseSpecTokenAtoms(RepoPaths.Requirements), ActualTokenKindNames()),
        CheckNativeMethodsAvoidKeywords(
            ParseArrayNativeMethodNames(RepoPaths.ArrayNatives),
            ParseSpecTokenAtoms(RepoPaths.Requirements)
                .Where(a => a.Section == "Keywords")
                .Select(a => a.Text)
                .ToHashSet(StringComparer.Ordinal)),
        CheckErrorCatalogGuardPresent(RepoPaths.RepoRoot()),
    ];
}
