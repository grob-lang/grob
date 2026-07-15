<#
.SYNOPSIS
    Local line-coverage gate. Runs the full test suite with coverlet and
    fails if total line coverage drops below the supplied threshold.

.DESCRIPTION
    SonarCloud's gate is on *new code* coverage, which can only be computed
    server-side. This script is a cheaper proxy: it gates overall coverage
    so a developer notices a large regression before pushing. Designed for
    use as a pre-push hook (slow) — never as pre-commit (which must stay fast).

    Threshold defaults to 80 — an interim ratchet on *overall* in-scope
    coverage, below the 90% line+branch floor D-328 actually mandates for
    that denominator. SonarCloud separately enforces its own *new-code* gate
    server-side on PR diffs — a different, unrelated check from D-328's
    overall floor. Bump this default toward 90 as overall coverage rises;
    D-328 is not satisfied until it reaches 90. Pass -Threshold 0 to emit the
    coverage report but skip enforcement.

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
    #
    # Test projects share transitive references (every project pulls in
    # Grob.Core; Grob.Integration.Tests pulls in the whole src/ graph via
    # Grob.Cli), so the same assembly is instrumented afresh in more than one
    # report — each report's own Summary reflects only *that* project's
    # narrower exercise of the shared code. Summing Summary totals per report
    # (the previous approach) therefore double- and triple-counts shared
    # modules' denominators and blends in each incidental, partial view,
    # dragging the aggregate below what any single project's dedicated suite
    # actually achieves. Deduplicating per sequence point — keyed on the
    # module/file/source-span identity, visited if *any* report visited it —
    # is the correct merge: a line is either covered by the test suite as a
    # whole or it is not, regardless of how many reports happen to mention it.
    $reports = Get-ChildItem -Path $resultsDir -Filter "coverage.opencover.xml" -Recurse -ErrorAction SilentlyContinue
    if (-not $reports) {
        throw "No coverage reports found under $resultsDir. Did 'dotnet test' actually run?"
    }

    $seen = @{}
    foreach ($r in $reports) {
        [xml]$xml = Get-Content $r.FullName
        foreach ($module in $xml.CoverageSession.Modules.Module) {
            $modulePath = $module.ModulePath

            $files = @{}
            if ($module.Files -and $module.Files.File) {
                foreach ($f in $module.Files.File) {
                    $files[$f.uid] = $f.fullPath
                }
            }

            foreach ($sp in $module.SelectNodes(".//SequencePoint")) {
                $filePath = if ($files.ContainsKey($sp.fileid)) { $files[$sp.fileid] } else { "?" }
                $key = "$modulePath|$filePath|$($sp.sl)|$($sp.sc)|$($sp.el)|$($sp.ec)"
                $visited = ([int]$sp.vc) -gt 0
                if (-not $seen.ContainsKey($key)) {
                    $seen[$key] = $visited
                } elseif ($visited) {
                    $seen[$key] = $true
                }
            }
        }
    }

    if ($seen.Count -eq 0) {
        throw "Coverage reports contained no sequence points."
    }

    $totalSequencePoints = $seen.Count
    $totalVisited = ($seen.Values | Where-Object { $_ }).Count

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
