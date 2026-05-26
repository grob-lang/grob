<#
.SYNOPSIS
    Run a full SonarCloud "new code" analysis against the local working tree.

.DESCRIPTION
    Mirrors .github/workflows/sonarcloud.yml so a developer can validate the
    Quality Gate before pushing. Begin → Restore → Build (Release) → Test with
    OpenCover coverage → End. Reports upload to the same SonarCloud project
    (grob-lang_grob) the CI workflow uses.

    Prerequisites:
      * .NET SDK on PATH (matches global.json)
      * Java 17+ on PATH (dotnet-sonarscanner is a Java process)
      * dotnet-sonarscanner installed:  dotnet tool install --global dotnet-sonarscanner
      * SONAR_TOKEN environment variable set to a token with "Execute Analysis"
        permission on the SonarCloud project.

    Usage:
      pwsh tooling/sonar-local.ps1
      pwsh tooling/sonar-local.ps1 -PullRequest 41 -BaseBranch main
#>

[CmdletBinding()]
param(
    [int]    $PullRequest,
    [string] $BaseBranch = "main",
    [string] $ProjectKey  = "grob-lang_grob",
    [string] $Organization = "grob-lang"
)

$ErrorActionPreference = "Stop"

if (-not $env:SONAR_TOKEN) {
    throw "SONAR_TOKEN environment variable is not set. Generate a user token at https://sonarcloud.io/account/security and `$env:SONAR_TOKEN = '<token>'."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repoRoot
try {
    # Run-scoped results directory so the scanner only imports coverage from
    # *this* invocation, never stale reports left under per-project TestResults.
    $resultsDir = Join-Path $repoRoot ".artifacts/sonar-local/TestResults"
    if (Test-Path $resultsDir) {
        Remove-Item $resultsDir -Recurse -Force
    }

    $beginArgs = @(
        "sonarscanner", "begin",
        "/k:$ProjectKey",
        "/o:$Organization",
        "/d:sonar.host.url=https://sonarcloud.io",
        "/d:sonar.token=$env:SONAR_TOKEN",
        "/d:sonar.cs.opencover.reportsPaths=.artifacts/sonar-local/TestResults/**/coverage.opencover.xml",
        "/d:sonar.coverage.exclusions=**/Grob.Cli/Program.cs,**/Grob.Lsp/Program.cs",
        "/d:sonar.test.exclusions=**/*.Tests/**"
    )
    if ($PullRequest) {
        $beginArgs += "/d:sonar.pullrequest.key=$PullRequest"
        $beginArgs += "/d:sonar.pullrequest.branch=$(git rev-parse --abbrev-ref HEAD)"
        $beginArgs += "/d:sonar.pullrequest.base=$BaseBranch"
    }

    Write-Host "==> sonarscanner begin" -ForegroundColor Cyan
    dotnet @beginArgs
    if ($LASTEXITCODE -ne 0) { throw "sonarscanner begin failed ($LASTEXITCODE)" }

    Write-Host "==> dotnet restore" -ForegroundColor Cyan
    dotnet restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed ($LASTEXITCODE)" }

    Write-Host "==> dotnet build (Release)" -ForegroundColor Cyan
    dotnet build --no-restore --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }

    Write-Host "==> dotnet test with OpenCover coverage" -ForegroundColor Cyan
    dotnet test --no-build --configuration Release `
        --results-directory $resultsDir `
        --collect:"XPlat Code Coverage" `
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
    if ($LASTEXITCODE -ne 0) { throw "dotnet test failed ($LASTEXITCODE)" }

    Write-Host "==> sonarscanner end" -ForegroundColor Cyan
    dotnet sonarscanner end "/d:sonar.token=$env:SONAR_TOKEN"
    if ($LASTEXITCODE -ne 0) { throw "sonarscanner end failed ($LASTEXITCODE)" }

    Write-Host ""
    Write-Host "Done. View results at https://sonarcloud.io/dashboard?id=$ProjectKey" -ForegroundColor Green
}
finally {
    Pop-Location
}
