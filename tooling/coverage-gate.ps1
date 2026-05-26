<#
.SYNOPSIS
    Local line-coverage gate. Runs the full test suite with coverlet and
    fails if total line coverage drops below the supplied threshold.

.DESCRIPTION
    SonarCloud's gate is on *new code* coverage, which can only be computed
    server-side. This script is a cheaper proxy: it gates overall coverage
    so a developer notices a large regression before pushing. Designed for
    use as a pre-push hook (slow) — never as pre-commit (which must stay fast).

    Threshold defaults to 80 to match the SonarCloud new-code gate. Pass
    -Threshold 0 to skip the gate and just emit the report.

    Output:
      * Per-project coverage files under <project>/TestResults/<guid>/coverage.opencover.xml
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

    $args = @(
        "test", "Grob.slnx",
        "--nologo",
        "--configuration", "Release",
        "--collect:XPlat Code Coverage",
        "--", "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover"
    )

    dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet test failed ($LASTEXITCODE)" }

    if ($Threshold -le 0) {
        Write-Host "Threshold gate skipped (Threshold <= 0)." -ForegroundColor Yellow
        return
    }

    # Aggregate line-coverage across all OpenCover reports.
    $reports = Get-ChildItem -Path "**/TestResults/**/coverage.opencover.xml" -Recurse -ErrorAction SilentlyContinue
    if (-not $reports) {
        throw "No coverage reports found under TestResults/. Did 'dotnet test' actually run?"
    }

    $totalVisited = 0
    $totalSequencePoints = 0
    foreach ($r in $reports) {
        [xml]$xml = Get-Content $r.FullName
        $summary = $xml.CoverageSession.Summary
        if ($summary) {
            $totalVisited         += [int]$summary.visitedSequencePoints
            $totalSequencePoints  += [int]$summary.numSequencePoints
        }
    }

    if ($totalSequencePoints -eq 0) {
        throw "Coverage reports contained no sequence points."
    }

    $pct = [math]::Round(100.0 * $totalVisited / $totalSequencePoints, 2)
    Write-Host ""
    Write-Host "Line coverage: $pct% ($totalVisited / $totalSequencePoints sequence points)" -ForegroundColor Cyan

    if ($pct -lt $Threshold) {
        Write-Host "FAIL — coverage $pct% is below threshold $Threshold%." -ForegroundColor Red
        exit 1
    }
    Write-Host "PASS — coverage $pct% meets threshold $Threshold%." -ForegroundColor Green
}
finally {
    Pop-Location
}
