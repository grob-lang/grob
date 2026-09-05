<#
.SYNOPSIS
    House-style prose check over staged markdown. Warn-only by design.

.DESCRIPTION
    Scans added lines in the staged diff for the Grob house-style rules that a
    reviewer otherwise catches by eye: serial commas, "simply" and friends,
    Americanisms, filler, emoji.

    This is a HIGH-RECALL, LOW-PRECISION detector and it must stay warn-only.

    The serial-comma pattern cannot distinguish a genuine Oxford comma from a
    legitimate three-clause sentence without parsing the list structure, which a
    regex cannot do. That is fine when the action is "print the line and let a
    human glance". It is NOT fine when the action is "refuse the commit". A false
    positive costs one second of reading; a blocked commit on a correct sentence
    costs trust in the whole gate, and a gate people learn to bypass catches
    nothing.

    Do not promote this to a blocking hook. If it starts producing more noise than
    signal, tighten the patterns or drop it — do not raise its authority.

    Scoped to added lines in *.md only, so a code-only commit pays nothing.

.PARAMETER Staged
    Check the staged diff (default). This is how the pre-commit hook invokes it.

.PARAMETER Path
    Check specific files instead of the staged diff. For ad-hoc use.

.EXAMPLE
    pwsh tooling/prose-check.ps1
    pwsh tooling/prose-check.ps1 -Path docs/design/grob-decisions-log.md
#>
[CmdletBinding()]
param(
    [switch]$Staged,
    [string[]]$Path
)

$ErrorActionPreference = 'Stop'

# --- rules -----------------------------------------------------------------
# Each rule: a regex, a label, and a note explaining what to check.
# 'Fuzzy' rules are the ones with known false positives.

$rules = @(
    @{
        Name  = 'serial comma'
        Regex = '\w,\s+[^,.;:!?]{1,55},\s+(and|or|nor)\s'
        Fuzzy = $true
        Note  = 'Looks like a list of three or more. Drop the comma before the conjunction. FALSE POSITIVE if these are independent clauses — remove the conjunction and see whether what remains is a list or two sentences.'
    },
    @{
        Name  = 'hedge word'
        Regex = '(?i)\b(simply|obviously|merely|of course)\b'
        Fuzzy = $false
        Note  = 'Tells a stuck reader they should not be stuck. Delete it; the sentence rarely needs replacing.'
    },
    @{
        Name  = 'Americanism'
        Regex = '(?<![A-Za-z_.])(?-i:[a-z]*(?:ize|izes|ized|izing|ization)|color|colors|behavior|behaviors|analyze|analyzed|catalog|defense|fulfill)(?![A-Za-z_(])'
        Fuzzy = $true
        Note  = 'Use British spelling in prose. FALSE POSITIVE on .NET and Grob identifiers (Initialize, Color, ErrorCatalog) — identifiers are never anglicised.'
    },
    @{
        Name  = 'filler'
        Regex = '(?i)(\bin order to\b|\bat this point in time\b|\bit is important to note\b|\bit should be noted\b)'
        Fuzzy = $false
        Note  = '"in order to" is "to". Delete the rest and start with the point.'
    },
    @{
        Name  = 'unspaced em dash'
        Regex = '\w--\w|\w—\w'
        Fuzzy = $false
        Note  = 'Em dashes are spaced in this corpus — like this.'
    },
    @{
        Name  = 'emoji'
        Regex = '[\u2190-\u21FF\u2600-\u27BF\uFE0F]|[\uD800-\uDBFF][\uDC00-\uDFFF]'
        Fuzzy = $false
        Note  = 'No emoji anywhere in the project.'
    }
)

# --- gather added lines ----------------------------------------------------

$targets = [System.Collections.Generic.List[object]]::new()

if ($Path) {
    foreach ($f in $Path) {
        if (-not (Test-Path $f)) { continue }
        $n = 0
        foreach ($line in (Get-Content -LiteralPath $f)) {
            $n++
            $targets.Add([pscustomobject]@{ File = $f; Line = $n; Text = $line })
        }
    }
}
else {
    $diff = & git diff --cached --unified=0 -- '*.md' 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'prose-check: not a git repository or git unavailable — skipping.'
        exit 0
    }

    $file = $null
    $lineNo = 0
    foreach ($line in $diff) {
        if ($line -match '^\+\+\+ b/(.+)$') { $file = $Matches[1]; continue }
        if ($line -match '^@@ -\d+(?:,\d+)? \+(\d+)') { $lineNo = [int]$Matches[1]; continue }
        if ($line -like '+++*' -or $line -like '---*') { continue }
        if ($line.StartsWith('+')) {
            $targets.Add([pscustomobject]@{ File = $file; Line = $lineNo; Text = $line.Substring(1) })
            $lineNo++
        }
    }
}

if ($targets.Count -eq 0) { exit 0 }

# --- scan ------------------------------------------------------------------

$hits = [System.Collections.Generic.List[object]]::new()
$inFence = $false

foreach ($t in $targets) {
    if ($t.Text -match '^\s*```') { $inFence = -not $inFence; continue }
    if ($inFence) { continue }                       # code blocks are not prose
    if ($t.Text -match '^\s*\|') { continue }        # tables are usually fragments
    if ($t.Text -match '^\s*(https?://|\[.*\]\()') { continue }

    # strip inline code before matching — identifiers are not prose
    $text = [regex]::Replace($t.Text, '`[^`]*`', ' ')
    if ([string]::IsNullOrWhiteSpace($text)) { continue }

    foreach ($rule in $rules) {
        if ($text -match $rule.Regex) {
            $hits.Add([pscustomobject]@{
                File = $t.File; Line = $t.Line; Rule = $rule.Name
                Fuzzy = $rule.Fuzzy; Note = $rule.Note; Text = $t.Text.Trim()
            })
        }
    }
}

if ($hits.Count -eq 0) { exit 0 }

# --- report ----------------------------------------------------------------

Write-Host ''
Write-Host "prose-check: $($hits.Count) candidate(s) in staged markdown. Warnings only — nothing is blocked."
Write-Host ''

foreach ($g in ($hits | Group-Object Rule)) {
    $r = $rules | Where-Object Name -eq $g.Name | Select-Object -First 1
    $tag = if ($r.Fuzzy) { ' (false positives expected)' } else { '' }
    Write-Host "  $($g.Name)$tag"
    Write-Host "    $($r.Note)"
    foreach ($h in $g.Group) {
        $snippet = if ($h.Text.Length -gt 110) { $h.Text.Substring(0, 110) + '...' } else { $h.Text }
        Write-Host "      $($h.File):$($h.Line)"
        Write-Host "        $snippet"
    }
    Write-Host ''
}

Write-Host 'Clear or consciously dismiss each one. See the house-style skill.'
Write-Host ''

exit 0   # warn-only, always. Do not change this.
