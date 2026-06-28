<#
.SYNOPSIS
    Local line-coverage gate. Runs the full test suite with coverlet and
    fails if total line coverage drops below the supplied threshold.

.DESCRIPTION
    SonarCloud's gate is on *new code* coverage, which can only be computed
    server-side. This script is a cheaper proxy: it gates overall coverage
    so a developer notices a large regression before pushing. Designed for
    use as a pre-push hook (slow) — never as pre-commit (which must stay fast).

    Threshold defaults to 80 — the current measured overall floor (D-328).
    This is a regression guard on *overall* coverage; the 90% gate on *new-code*
    coverage is enforced in CI by SonarCloud (server-side only).  Bump this
    default as overall coverage rises.  Pass -Threshold 0 to emit the coverage
    report but skip enforcement.

    Output:
      * Per-project coverage files under
        .artifacts/coverage-gate/TestResults/<guid>/coverage.opencover.xml
      * Aggregated console summary printed to stdout
#>

[CmdletBinding()]
param(
    [int] $Threshold = 80
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repoRoot
try {
    Write-Host "==> dotnet test with coverlet (threshold=$Threshold)" -ForegroundColor Cyan

    # Run-scoped results directory so we never aggregate stale reports from
    # earlier runs of `dotnet test` (which would skew the gate percentage).
    $resultsDir = Join-Path $repoRoot ".artifacts/coverage-gate/TestResults"
    if (Test-Path $resultsDir) {
        Remove-Item $resultsDir -Recurse -Force
    }

    # Include and ExcludeByFile mirror sonar.coverage.exclusions in
    # .github/workflows/sonarcloud.yml so the local percentage tracks what
    # SonarCloud actually measures.  Include must name the CLI assembly
    # explicitly ('grob', OutputType=Exe) because coverlet may miss
    # non-standard Exe assembly names without an explicit Include pattern.
    $include = "[grob]*,[Grob.*]*"
    $excludeByFile = "**/Grob.Cli/Program.cs,**/Grob.Lsp/Program.cs"

    $testArgs = @(
        "test", "Grob.slnx",
        "--nologo",
        "--configuration", "Release",
        "--results-directory", $resultsDir,
        "--collect:XPlat Code Coverage",
        "--",
        "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover",
        "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include=$include",
        "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByFile=$excludeByFile"
    )

    dotnet @testArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet test failed ($LASTEXITCODE)" }

    # Aggregate line-coverage across all OpenCover reports for *this* run.
    $reports = Get-ChildItem -Path $resultsDir -Filter "coverage.opencover.xml" -Recurse -ErrorAction SilentlyContinue
    if (-not $reports) {
        throw "No coverage reports found under $resultsDir. Did 'dotnet test' actually run?"
    }

    $totalVisited = 0
    $totalSequencePoints = 0
    foreach ($r in $reports) {
        [xml]$xml = Get-Content $r.FullName
        $summary = $xml.CoverageSession.Summary
        if ($summary) {
            $totalVisited        += [int]$summary.visitedSequencePoints
            $totalSequencePoints += [int]$summary.numSequencePoints
        }
    }

    if ($totalSequencePoints -eq 0) {
        throw "Coverage reports contained no sequence points."
    }

    $pct = [math]::Round(100.0 * $totalVisited / $totalSequencePoints, 2)
    Write-Host ""
    Write-Host "Line coverage: $pct% ($totalVisited / $totalSequencePoints sequence points)" -ForegroundColor Cyan

    if ($Threshold -le 0) {
        Write-Host "Threshold gate skipped (Threshold <= 0)." -ForegroundColor Yellow
        return
    }

    if ($pct -lt $Threshold) {
        Write-Host "FAIL — coverage $pct% is below threshold $Threshold%." -ForegroundColor Red
        exit 1
    }
    Write-Host "PASS — coverage $pct% meets threshold $Threshold%." -ForegroundColor Green
}
finally {
    Pop-Location
}
