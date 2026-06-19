using System.Text;

using Grob.Compiler;
using Grob.Core;
using Grob.Vm;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 4 Increment D end-to-end tests — the <c>select</c>/<c>case</c> statement.
/// Each test drives the full pipeline: Lexer → Parser → TypeChecker → Compiler → VM.
/// </summary>
/// <remarks>
/// Covers first-match with no fall-through, multi-value cases, the optional
/// <c>default</c>, the non-exhaustive no-op (D-301), and the D-315 control-flow
/// rules: <c>continue</c> inside a <c>select</c> passes through to the enclosing
/// loop, while a <c>break</c> in the loop body (outside the <c>select</c>) exits the
/// loop normally.
/// </remarks>
public sealed class Sprint4IncrementDTests {
    private static string Run(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"Pipeline produced unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors,
            $"Compiler produced unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        var output = new StringWriter(new StringBuilder());
        var vm = new VirtualMachine(output);
        vm.Run(chunk);
        return output.ToString();
    }

    private static string NL => Environment.NewLine;

    [Fact]
    public void Select_FirstMatchRuns_NoFallThrough() {
        string stdout = Run("""
            select (2) {
                case 1 {
                    print(10)
                }
                case 2 {
                    print(20)
                }
                case 2 {
                    print(21)
                }
                default {
                    print(0)
                }
            }
            """);
        Assert.Equal($"20{NL}", stdout);
    }

    [Fact]
    public void Select_MultiValueCase_MatchesEitherValue() {
        string stdout = Run("""
            select (3) {
                case 1, 2, 3 {
                    print(123)
                }
                default {
                    print(0)
                }
            }
            """);
        Assert.Equal($"123{NL}", stdout);
    }

    [Fact]
    public void Select_NoMatchNoDefault_IsANoOp() {
        string stdout = Run("""
            select (9) {
                case 1 {
                    print(1)
                }
            }
            print(99)
            """);
        Assert.Equal($"99{NL}", stdout);
    }

    [Fact]
    public void Select_OnStringSubject_MatchesByValue() {
        string stdout = Run("""
            verb := "stop"
            select (verb) {
                case "go" {
                    print(1)
                }
                case "stop" {
                    print(2)
                }
                default {
                    print(0)
                }
            }
            """);
        Assert.Equal($"2{NL}", stdout);
    }

    /// <summary>
    /// A <c>select</c> with a multi-value case and a <c>default</c>, inside a
    /// <c>while</c> whose body uses <c>continue</c> inside the <c>select</c>. The
    /// <c>continue</c> passes through to the <c>while</c> (D-315), skipping the
    /// trailing <c>print</c> on that iteration.
    /// </summary>
    [Fact]
    public void Select_InsideWhile_ContinuePassesThroughToLoop() {
        string stdout = Run("""
            i := 0
            while (i < 5) {
                i = i + 1
                select (i) {
                    case 2 {
                        continue
                    }
                    case 1, 3 {
                        print(i)
                    }
                    default {
                        print(0)
                    }
                }
                print(99)
            }
            """);
        // i=1 -> case 1,3 -> 1, then 99
        // i=2 -> continue -> (skips 99)
        // i=3 -> case 1,3 -> 3, then 99
        // i=4 -> default  -> 0, then 99
        // i=5 -> default  -> 0, then 99
        Assert.Equal($"1{NL}99{NL}3{NL}99{NL}0{NL}99{NL}0{NL}99{NL}", stdout);
    }

    /// <summary>
    /// A <c>break</c> in the <c>while</c> body (outside the <c>select</c>) exits the
    /// loop normally, even though the loop body contains a <c>select</c> with a
    /// multi-value case and a <c>default</c>.
    /// </summary>
    [Fact]
    public void Select_InsideWhile_BreakInLoopBodyExitsLoop() {
        string stdout = Run("""
            i := 0
            while (i < 10) {
                i = i + 1
                select (i) {
                    case 1, 2 {
                        print(i)
                    }
                    default {
                        print(0)
                    }
                }
                if (i == 3) {
                    break
                }
            }
            print(-1)
            """);
        // i=1 -> 1; i=2 -> 2; i=3 -> default 0 then break; then -1
        Assert.Equal($"1{NL}2{NL}0{NL}-1{NL}", stdout);
    }
}
