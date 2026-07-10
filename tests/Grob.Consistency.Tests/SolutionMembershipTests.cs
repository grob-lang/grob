using Xunit;

namespace Grob.Consistency.Tests;

/// <summary>
/// Test-project membership gate (D-335). PR #94 dropped
/// <c>tests/Grob.Integration.Tests</c> from <c>Grob.slnx</c> for two sprints without
/// tripping any check. <see cref="SolutionMembership.FindOrphans"/> is the pure
/// comparison this guards with; <see cref="SolutionMembershipLiveTests"/> runs it
/// against the real repo.
/// </summary>
public sealed class SolutionMembershipTests {
    [Fact]
    public void FindOrphans_ReturnsMissingPath_WhenSlnxOmitsADiscoveredCsproj() {
        const string slnx = """
            <Solution>
              <Folder Name="/tests/">
                <Project Path="tests\Grob.Core.Tests\Grob.Core.Tests.csproj" />
              </Folder>
            </Solution>
            """;
        var discovered = new[] {
            @"tests\Grob.Core.Tests\Grob.Core.Tests.csproj",
            @"tests\Grob.Integration.Tests\Grob.Integration.Tests.csproj",
        };

        var orphans = SolutionMembership.FindOrphans(slnx, discovered);

        var orphan = Assert.Single(orphans);
        Assert.Equal(@"tests\Grob.Integration.Tests\Grob.Integration.Tests.csproj", orphan);
    }

    [Fact]
    public void FindOrphans_ReturnsEmpty_WhenSlnxReferencesEveryDiscoveredPath() {
        const string slnx = """
            <Solution>
              <Folder Name="/tests/">
                <Project Path="tests\Grob.Core.Tests\Grob.Core.Tests.csproj" />
                <Project Path="tests\Grob.Integration.Tests\Grob.Integration.Tests.csproj" />
              </Folder>
            </Solution>
            """;
        var discovered = new[] {
            @"tests\Grob.Core.Tests\Grob.Core.Tests.csproj",
            @"tests\Grob.Integration.Tests\Grob.Integration.Tests.csproj",
        };

        var orphans = SolutionMembership.FindOrphans(slnx, discovered);

        Assert.Empty(orphans);
    }

    [Fact]
    public void FindOrphans_ComparesPathsCaseInsensitivelyAndSeparatorNormalised() {
        const string slnx = """
            <Solution>
              <Folder Name="/tooling/">
                <Project Path="tooling/Grob.BenchCheck.Tests/GROB.BENCHCHECK.TESTS.csproj" />
              </Folder>
            </Solution>
            """;
        var discovered = new[] {
            @"tooling\Grob.BenchCheck.Tests\Grob.BenchCheck.Tests.csproj",
        };

        var orphans = SolutionMembership.FindOrphans(slnx, discovered);

        Assert.Empty(orphans);
    }
}
