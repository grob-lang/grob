using System.Text;

namespace Grob.Compiler;

/// <summary>
/// Decodes the Grob string escape sequences a <see cref="Ast.StringTextPart"/> stores raw.
/// Shared by the compiler, which emits the decoded text, and the type checker, which
/// validates a double-quoted regex pattern (E0602) as it will run rather than as it was
/// written (D-440).
/// </summary>
internal static class StringEscapes {
    /// <summary>
    /// Decodes <paramref name="raw"/>. Recognised sequences: <c>\n \r \t \\ \" \$</c>. Any
    /// other <c>\x</c> is passed through unchanged; the lexer has already reported E2006.
    /// </summary>
    internal static string Decode(string raw) {
        if (!raw.Contains('\\')) return raw;

        var sb = new StringBuilder(raw.Length);
        int i = 0;
        while (i < raw.Length) {
            if (raw[i] == '\\' && i + 1 < raw.Length) {
                char next = raw[i + 1];
                switch (next) {
                    case 'n': sb.Append('\n'); i += 2; break;
                    case 'r': sb.Append('\r'); i += 2; break;
                    case 't': sb.Append('\t'); i += 2; break;
                    case '\\': sb.Append('\\'); i += 2; break;
                    case '"': sb.Append('"'); i += 2; break;
                    case '$': sb.Append('$'); i += 2; break;
                    default:
                        // Unknown escape: pass both chars through; lexer already
                        // emitted a diagnostic, so this path is error-recovery only.
                        sb.Append(raw[i]);
                        i++;
                        break;
                }
            } else {
                sb.Append(raw[i]);
                i++;
            }
        }
        return sb.ToString();
    }
}
