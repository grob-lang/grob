using Grob.DriftCheck;
using Xunit;

namespace Grob.Consistency.Tests;

/// <summary>
/// The live guard for D-335: every <c>*.Tests.csproj</c> discovered on disk must be
/// referenced by <c>Grob.slnx</c>. Passes against the current tree; fails the build the
/// moment a test project is dropped from the solution or added without a matching entry.
/// </summary>
public sealed class SolutionMembershipLiveTests {
    [Fact]
    public void LiveGuard_EveryDiscoveredTestProject_IsReferencedInSlnx() {
        var repoRoot = RepoPaths.RepoRoot();
        var slnxPath = Path.Join(repoRoot, "Grob.slnx");
        var slnxXml = File.ReadAllText(slnxPath);

        var discovered = SolutionMembership.DiscoverTestProjects(repoRoot);
        Assert.True(discovered.Count > 0, "Discovered zero *.Tests.csproj files — the enumeration may be broken.");

        var orphans = SolutionMembership.FindOrphans(slnxXml, discovered);

        Assert.True(orphans.Count == 0,
            "Test project(s) present on disk but not referenced by Grob.slnx: " +
            string.Join(", ", orphans));
    }
}
