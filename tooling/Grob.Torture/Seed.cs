namespace Grob.Torture;

/// <summary>One program the oracle starts from. A seed that does not compile is still a seed.</summary>
/// <param name="Id">
/// The seed's stable id, <c>&lt;origin&gt;@&lt;FNV-1a of the source&gt;</c>: it changes only when the
/// file moves or the program's text changes, and then the known-findings list must be triaged again.
/// </param>
/// <param name="Kind">Where the seed came from.</param>
/// <param name="Origin">The repository-relative path it was read from, with forward slashes.</param>
/// <param name="Source">The program text, with line feeds only.</param>
internal sealed record Seed(string Id, SeedKind Kind, string Origin, string Source);
