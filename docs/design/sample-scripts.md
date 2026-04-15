# Grob — Sample Scripts & API Surface Validation

> This document is the living bible for Grob’s MVP API surface.
> Each entry shows a real script from PS, bash, or Go — then the Grob equivalent.
> Grob scripts use the language and stdlib as currently designed.
> Gaps, assumptions, and confidence ratings are explicit.
> 
> **Confidence ratings:**
> 
> - ⭐⭐⭐ Solid — language and stdlib fully designed, Grob script is authoritative
> - ⭐⭐ Likely — design mostly locked, minor assumptions made
> - ⭐ Gap — design decisions needed before this is implementable
> 
> **Annotations:**
> 
> - `[ASSUMPTION]` — reasonable guess at stdlib shape, not yet locked
> - `[GAP]` — feature or module not yet designed
> - `[NOISE]` — original script complexity that Grob sidesteps by design

-----

## Script 1 — Bulk File Rename by Pattern

**Use case:** Hobbyist / sysadmin. Rename a set of files matching a pattern — the
“day one” Grob use case from the design documents. People reach for PowerShell or
bash for this constantly.

**Confidence:** ⭐⭐⭐

-----

### PowerShell original

```powershell
$dir    = "C:\Reports"
$from   = "2024"
$to     = "2025"

Get-ChildItem -Path $dir -File | Where-Object { $_.Name -like "*$from*" } | ForEach-Object {
    $newName = $_.Name -replace $from, $to
    Rename-Item -Path $_.FullName -NewName $newName
    Write-Host "Renamed: $($_.Name) → $newName"
}
```

**Notes:** `Get-ChildItem | Where-Object | ForEach-Object` is the standard PS
pipeline. Readable enough but the verb-noun ceremony is verbose. `-like` vs
`-replace` are different operators doing similar string jobs.

-----

### Grob equivalent

```grob
param dir:  string = "C:\\Reports"
param from: string
param to:   string

for file in fs.list(dir) {
    if (file.name.contains(from)) {
        new_name := file.name.replace(from, to)
        file.rename(new_name)
        print("Renamed: ${file.name} → ${new_name}")
    }
}
```

**What this demonstrates:**

- `param` block for reusable, parameterised scripts
- `fs.list()` returning typed file objects with `.name`, `.rename()`
- Fluent string methods — `.contains()`, `.replace()`
- `for...in` loop over a collection
- String interpolation

**Gaps noted:** None — this script uses only confirmed language features and stdlib.

-----

## Script 2 — Organise Photos by Date

**Use case:** Hobbyist. Move photos from a download folder into year/month subfolders
based on file date. One of the most common personal automation tasks on GitHub.

**Confidence:** ⭐⭐⭐

-----

### PowerShell original

```powershell
param (
    [string]$SourceDir = "C:\Users\Chris\Downloads\Photos",
    [string]$DestDir   = "C:\Photos"
)

Get-ChildItem -Path $SourceDir -Include *.jpg,*.jpeg,*.png,*.cr2 -Recurse |
ForEach-Object {
    $date    = $_.LastWriteTime
    $year    = $date.Year.ToString()
    $month   = $date.ToString("MM-MMMM")
    $destSub = Join-Path $DestDir (Join-Path $year $month)

    if (-not (Test-Path $destSub)) {
        New-Item -ItemType Directory -Path $destSub | Out-Null
    }

    $dest = Join-Path $destSub $_.Name
    Move-Item -Path $_.FullName -Destination $dest
    Write-Host "Moved $($_.Name) → $destSub"
}
```

**Notes:** Solid script but noisy. `Join-Path` repeated, `Test-Path` / `New-Item`
for directory creation, `Out-Null` to suppress output. The intent is clear but
the ceremony obscures it.

-----

### Grob equivalent

```grob
param source_dir: string = "C:\\Users\\Chris\\Downloads\\Photos"
param dest_dir:   string = "C:\\Photos"

const extensions := [".jpg", ".jpeg", ".png", ".cr2"]

for file in fs.list(source_dir, recursive: true) {
    if (extensions.contains(file.extension)) {
        year     := file.modified.year.toString()
        month    := file.modified.format("MM-MMMM")
        dest_sub := path.join(dest_dir, year, month)

        fs.ensureDir(dest_sub)
        file.moveTo(dest_sub)
        print("Moved ${file.name} → ${dest_sub}")
    }
}
```

**What this demonstrates:**

- `fs.list()` with named parameter `recursive: true`
- File object properties — `.extension`, `.modified` (a `date` value)
- `date` properties — `.year`, `.format(pattern)`
- `path.join()` with multiple segments (variadic)
- `fs.ensureDir()` — creates recursively, no-op if exists
- Array `.contains()` method

**Gaps noted:** None — this script uses only confirmed language features and stdlib.

-----

## Script 3 — Find Large Files and Report

**Use case:** Sysadmin / hobbyist. Find files over a size threshold in a directory
tree, report them sorted by size. Classic disk space investigation script.

**Confidence:** ⭐⭐⭐

-----

### PowerShell original

```powershell
param (
    [string]$Path      = "C:\",
    [int]   $ThresholdMB = 100
)

$threshold = $ThresholdMB * 1MB

Get-ChildItem -Path $Path -Recurse -File -ErrorAction SilentlyContinue |
Where-Object { $_.Length -gt $threshold } |
Sort-Object Length -Descending |
Select-Object Name, DirectoryName, @{Name="SizeMB"; Expression={[math]::Round($_.Length / 1MB, 2)}} |
Format-Table -AutoSize
```

**Notes:** Excellent PS one-liner in spirit. `-ErrorAction SilentlyContinue` is doing
quiet error suppression — a common PS-ism that Grob handles differently. The
`@{Name=...; Expression=...}` calculated property syntax is powerful but arcane.

-----

### Grob equivalent

```grob
param path:         string = "C:\\"
param threshold_mb: int    = 100

type FileEntry {
    name:    string
    folder:  string
    size_mb: float
}

threshold := threshold_mb * 1024 * 1024

entries := fs.list(path, recursive: true)
    .filter(f => f.size > threshold)
    .map(f => FileEntry {
        name:    f.name
        folder:  f.directory
        size_mb: (f.size / 1024.0 / 1024.0).round(2)
    })

print(
    entries
        .sort(e => e.size_mb, descending: true)
        .format.table()
)
```

**What this demonstrates:**

- Typed struct projection via `type` and inline construction
- Fluent chain — `filter`, `map`, `sort`, `format.table()`
- `format.table()` as the output step — replaces PS’s `Format-Table -AutoSize`
- Named parameter `descending: true` on sort
- No error suppression needed — `fs.list()` throws `IoError` on access denied,
  catchable if needed

**Gaps noted:** None — this script uses only confirmed language features and stdlib.

-----

## Script 4 — GitHub Repos Backup

**Use case:** Developer hobbyist. Clone all repos from a GitHub organisation or
user to a local backup folder. Common personal backup script.

**Confidence:** ⭐⭐⭐

-----

### Bash original

```bash
#!/bin/bash
# Requires: curl, jq, git
ORG="myorg"
TOKEN="${GITHUB_TOKEN}"
BACKUP_DIR="$HOME/github-backup"
mkdir -p "$BACKUP_DIR"

repos=$(curl -s -H "Authorization: token $TOKEN" \
  "https://api.github.com/orgs/$ORG/repos?per_page=100" | \
  jq -r '.[].ssh_url')

for repo in $repos; do
  name=$(basename "$repo" .git)
  dest="$BACKUP_DIR/$name"
  if [ -d "$dest" ]; then
    echo "Updating $name..."
    git -C "$dest" pull
  else
    echo "Cloning $name..."
    git clone "$repo" "$dest"
  fi
done
```

**Notes:** Requires `curl`, `jq`, and `git` as external dependencies — none of which
the script declares or validates. `jq` for JSON parsing inline in bash is a common
but fragile pattern. The `$(basename "$repo" .git)` is pure bash noise.

-----

### Grob equivalent

```grob
import Grob.Http   // http.* and auth.* both available

@secure
param token: string

param org:        string
param backup_dir: string = "C:\\github-backup"

type Repo {
    name:    string
    ssh_url: string
}

fs.ensureDir(backup_dir)

token_header := auth.bearer(token)
url          := "https://api.github.com/orgs/${org}/repos?per_page=100"
repos        := http.get(url, token_header).asJson().mapAs<Repo>()

for repo in repos {
    dest := path.join(backup_dir, repo.name)

    if (fs.exists(dest)) {
        print("Updating ${repo.name}...")
        process.runOrFail("git", ["-C", dest, "pull"])
    } else {
        print("Cloning ${repo.name}...")
        process.runOrFail("git", ["clone", repo.ssh_url, dest])
    }
}
```

**What this demonstrates:**

- `@secure` param for the GitHub token — never appears in params file
- `Grob.Http` for authenticated API call — `auth.*` available as sub-namespace
- `.asJson().mapAs<Repo>()` — typed deserialisation
- `fs.exists()` for conditional logic
- `fs.ensureDir()` for directory creation
- `process.runOrFail()` — preferred safe form for git invocation
- No external `jq` dependency — `json` is core stdlib

**Gaps noted:** None — this script uses only confirmed language features and stdlib.

**Confidence:** ⭐⭐⭐

-----

## Script 5 — CSV Data Processing / Report

**Use case:** Sysadmin / business user. Read a CSV exported from Excel (e.g. a list
of employees or assets), process it, and output a filtered report. Classic Excel →
script workflow.

**Confidence:** ⭐⭐⭐

-----

### PowerShell original

```powershell
param (
    [string]$InputFile  = ".\staff.csv",
    [string]$Department = "Engineering",
    [int]   $MaxSalary  = 60000
)

Import-Csv $InputFile |
Where-Object { $_.Department -eq $Department -and [int]$_.Salary -lt $MaxSalary } |
Select-Object Name, JobTitle, Salary, StartDate |
Sort-Object Salary -Descending |
Format-Table -AutoSize
```

**Notes:** PS CSV handling is genuinely good here. `Import-Csv` with headers is
clean. The `[int]$_.Salary` type cast is necessary because PS reads everything as
strings — Grob’s `mapAs<T>()` handles this at the boundary.

-----

### Grob equivalent

```grob
param input_file:  string = ".\\staff.csv"
param department:  string = "Engineering"
param max_salary:  int    = 60000

type Employee {
    name:       string
    job_title:  string
    department: string
    salary:     int
    start_date: string
}

print(
    csv.read(input_file).mapAs<Employee>()
        .filter(e => e.department == department && e.salary < max_salary)
        .select(e => #{ name: e.name, job_title: e.job_title, salary: e.salary, start_date: e.start_date })
        .sort(e => e.salary, descending: true)
        .format.table()
)
```

**What this demonstrates:**

- `csv.read()` as primary file input — no pipe syntax needed
- `mapAs<Employee>()` — typed deserialisation from CSV, int fields auto-converted
- Fluent `filter`, `select`, `sort`, `format.table()` chain
- `select()` as a typed projection — equivalent to PS `Select-Object`
- `#{ }` anonymous struct literal syntax in projection
- Significantly less ceremony than the PS equivalent — no type casts, no `-and`
  operator in a different syntax

**Gaps noted:** None — this script uses only confirmed language features and stdlib.

-----

## Script 6 — Azure CLI Wrapper / Bicep Deployment

**Use case:** DevOps / cloud sysadmin. Deploy a Bicep template to Azure, checking
for prerequisites, capturing output, logging results. Real-world Azure automation.

**Confidence:** ⭐⭐⭐

-----

### PowerShell original

```powershell
param (
    [Parameter(Mandatory=$true)] [string]$ResourceGroup,
    [Parameter(Mandatory=$true)] [string]$Location,
    [string]$TemplatePath = ".\main.bicep",
    [string]$Environment  = "staging"
)

Write-Host "Deploying to $ResourceGroup in $Location..."

# Check az CLI is available
try {
    az version | Out-Null
} catch {
    Write-Error "Azure CLI not found. Install from https://aka.ms/installazurecliwindows"
    exit 1
}

# Build the Bicep template
az bicep build --file $TemplatePath
if ($LASTEXITCODE -ne 0) {
    Write-Error "Bicep build failed"
    exit 1
}

# Deploy
$output = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file $TemplatePath `
    --parameters environment=$Environment `
    --query "properties.outputs" `
    --output json

Write-Host "Deployment complete"
Write-Host $output
```

**Notes:** Solid real-world script. The `$LASTEXITCODE -ne 0` pattern is PS’s
way of checking process exit codes — Grob handles this with `runOrFail`. The
backtick line continuation is PS-specific noise.

-----

### Grob equivalent

```grob
param resource_group: string
param location:       string
param template_path:  string = ".\\main.bicep"
param environment:    string = "staging"

log.info("Deploying to ${resource_group} in ${location}...")

// Verify az CLI is available
try {
    process.runOrFail("az", ["version"])
} catch e {
    log.error("Azure CLI not found. Install from https://aka.ms/installazurecliwindows")
    exit(1)
}

// Build Bicep template
process.runOrFail("az", ["bicep", "build", "--file", template_path])

// Deploy and capture output
result := process.run("az", [
    "deployment", "group", "create",
    "--resource-group", resource_group,
    "--template-file",  template_path,
    "--parameters",     "environment=${environment}",
    "--query",          "properties.outputs",
    "--output",         "json"
])

if (result.exitCode != 0) {
    log.error("Deployment failed: ${result.stderr}")
    exit(1)
} else {
    log.info("Deployment complete")
    print(result.stdout)
}
```

**What this demonstrates:**

- Required params with no default (`resource_group`, `location`)
- `log.info()` and `log.error()` for structured output vs `print()` for data
- `try/catch` for prerequisite checking
- `process.runOrFail()` for commands that must succeed
- `process.run()` with result capture for commands where output matters
- `result.exitCode`, `result.stdout`, `result.stderr` on `ProcessResult`
- `exit(1)` to signal failure to calling process
- No backtick line continuation — implicit continuation on open bracket

**Gaps noted:**

- None — this script uses only confirmed language features and stdlib

-----

## Script 7 — REST API Data Pull and JSON Report

**Use case:** Developer / sysadmin. Hit a REST API, parse the JSON response, filter
and format results. Represents the ADO / GitHub / any-API pattern.

**Confidence:** ⭐⭐⭐

-----

### Bash original (with curl + jq)

```bash
#!/bin/bash
# List GitHub issues older than 30 days for a repo

REPO="myorg/myrepo"
TOKEN="${GITHUB_TOKEN}"
CUTOFF=$(date -d "30 days ago" --iso-8601)

response=$(curl -s \
  -H "Authorization: Bearer $TOKEN" \
  -H "Accept: application/vnd.github.v3+json" \
  "https://api.github.com/repos/$REPO/issues?state=open&per_page=100")

echo "$response" | jq -r \
  --arg cutoff "$CUTOFF" \
  '.[] | select(.pull_request == null) |
   select(.created_at < $cutoff) |
   "\(.number)\t\(.title)\t\(.created_at)\t\(.user.login)"' |
  column -t -s $'\t'
```

**Notes:** This is why people reach for something better than bash. The `jq`
filter is powerful but opaque — `.[] | select(.pull_request == null)` to exclude
PRs from issues is a known jq idiom that you either know or you don’t. The
`column -t` for table formatting is another external dependency. No types anywhere.

-----

### Grob equivalent

```grob
import Grob.Http   // http.* and auth.* both available

@secure
param token: string

param repo:      string
param days_old:  int = 30

type Issue {
    number:     int
    title:      string
    created_at: string
    state:      string
    user:       IssueUser
}

type IssueUser {
    login: string
}

cutoff := date.today().minusDays(days_old)

issues := http.get(
    "https://api.github.com/repos/${repo}/issues?state=open&per_page=100",
    auth.bearer(token)
).asJson().mapAs<Issue>()

print(
    issues
        .filter(i => date.parse(i.created_at) < cutoff)
        .select(i => #{
            number: i.number
            title:  i.title
            age:    date.parse(i.created_at).daysUntil(date.today())
            author: i.user.login
        })
        .format.table()
)
```

**What this demonstrates:**

- Nested struct types (`Issue` containing `IssueUser`)
- `date.parse()` used inline in filter expression
- `date` comparison directly — no string manipulation needed
- `daysUntil()` for clean age-in-days computation
- `.select()` projection with renamed/computed fields
- `format.table()` replacing `jq | column -t`
- No `jq` dependency — json is core, HTTP is a first-party plugin

**Gaps noted:** None — nested struct access (`i.user.login`) confirmed in decisions log.

**Confidence:** ⭐⭐⭐

-----

## Script 8 — Stale Git Branches Report

**Use case:** Developer / team lead. Find branches in a local Git repo that haven’t
been updated in N days. Common repo hygiene script.

**Confidence:** ⭐⭐⭐

-----

### PowerShell original

```powershell
param (
    [string]$RepoPath  = ".",
    [int]   $StaleDays = 30
)

$cutoff = (Get-Date).AddDays(-$StaleDays).ToString("yyyy-MM-dd")

$branches = git -C $RepoPath branch -r --format="%(refname:short)|%(committerdate:iso)|%(authorname)" |
    ConvertFrom-Csv -Delimiter "|" -Header "Branch","Date","Author" |
    Where-Object { $_.Date -lt $cutoff -and $_.Branch -notmatch "HEAD" }

$branches | Select-Object Branch, Date, Author | Format-Table -AutoSize
```

**Notes:** Using `git` output piped to `ConvertFrom-Csv` is a clever PS-ism —
treating git’s formatted output as a pseudo-CSV. Gets the job done but is fragile
if git’s output format changes.

-----

### Grob equivalent

```grob
param repo_path: string = "."
param stale_days: int   = 30

type BranchInfo {
    branch: string
    date:   string
    author: string
}

cutoff := date.today().minusDays(stale_days)

raw := process.runOrFail("git", [
    "-C", repo_path,
    "branch", "-r",
    "--format=%(refname:short)|%(committerdate:iso)|%(authorname)"
])

print(
    raw.stdout
        .split("\n")
        .filter(line => line.length > 0)
        .map(line => {
            parts  := line.split("|")
            BranchInfo {
                branch: parts[0],
                date:   parts[1],
                author: parts[2]
            }
        })
        .filter(b => !b.branch.contains("HEAD") && date.parse(b.date) < cutoff)
        .format.table()
)
```

**What this demonstrates:**

- `process.runOrFail()` capturing stdout
- String split and inline processing
- Named struct construction inside `.map()` lambda
- Array indexing `parts[0]`
- Chained filter, map, filter pattern
- `date.parse()` on an ISO string from git output

**Note:** This script could also be written using `csv.parse(raw.stdout, delimiter: "|")`
for the structured parsing step — both approaches are valid.

**Gaps noted:** None — this script uses only confirmed language features and stdlib.

**Confidence:** ⭐⭐⭐

-----

## Script 9 — Disk Space Monitor with Log

**Use case:** Sysadmin / home server user. Check disk usage on one or more drives,
log warnings if above threshold, output a summary. Scheduled task / cron pattern.

**Confidence:** ⭐⭐

-----

### PowerShell original

```powershell
param (
    [int]$WarnPercent = 80,
    [int]$CritPercent = 90
)

$drives = Get-PSDrive -PSProvider FileSystem | Where-Object { $_.Used -gt 0 }

foreach ($drive in $drives) {
    $total   = $drive.Used + $drive.Free
    $usedPct = [math]::Round(($drive.Used / $total) * 100, 1)
    $status  = if ($usedPct -ge $CritPercent) { "CRITICAL" }
               elseif ($usedPct -ge $WarnPercent) { "WARNING" }
               else { "OK" }

    $msg = "$($drive.Name): ${usedPct}% used ($status)"
    if ($status -eq "CRITICAL") { Write-Warning $msg }
    elseif ($status -eq "WARNING") { Write-Warning $msg }
    else { Write-Host $msg }
}
```

**Notes:** `Get-PSDrive -PSProvider FileSystem` is Windows-specific. The inline
`if/elseif` as expression is idiomatic PS. `Write-Warning` vs `Write-Host` is
PS’s way of directing to different output streams — Grob’s `log` module handles
this more explicitly.

-----

### Grob equivalent

```grob
param warn_percent: int = 80
param crit_percent: int = 90

type DriveStatus {
    name:     string
    used_pct: float
    status:   string
}

// [GAP] No disk/drive introspection in current stdlib design
// This script requires a system info module or process-based approach

drives := process.run("powershell", [
    "-Command",
    "Get-PSDrive -PSProvider FileSystem | Where-Object { $_.Used -gt 0 } | ConvertTo-Json"
]).stdout

// [ASSUMPTION] parsing PowerShell JSON output
type PsDrive {
    name: string
    used: int
    free: int
}

ps_drives := json.parse(drives).mapAs<PsDrive[]>()   // [ASSUMPTION] array from JSON

for d in ps_drives {
    total    := d.used + d.free
    used_pct := ((d.used.toFloat() / total.toFloat()) * 100.0).round(1)
    status   := used_pct switch {
        >= crit_percent => "CRITICAL",
        >= warn_percent => "WARNING",
        _               => "OK"
    }

    msg := "${d.name}: ${used_pct}% used (${status})"
    if (status == "CRITICAL" || status == "WARNING") {
        log.warning(msg)
    } else {
        log.info(msg)
    }
}
```

**What this demonstrates:**

- `process.run()` to call PowerShell for system information
- `log.warning()` and `log.info()` for different severity streams
- Switch expression for multi-branch value selection
- Float arithmetic and rounding

**Gaps noted:**

- **Major gap:** No system/disk introspection module in Grob stdlib `[GAP]`.
  The script above is a workaround via PowerShell — which defeats the purpose.
  A future `sys` module with `sys.drives()` would resolve this cleanly.
- `json.parse(str).mapAs<PsDrive[]>()` — parsing to an array type `[ASSUMPTION]`

-----

## Script 10 — Download and Verify a File

**Use case:** Developer / hobbyist. Download a file from a URL, verify its checksum,
report success or failure. Common setup/bootstrap automation.

**Confidence:** ⭐⭐⭐

-----

### PowerShell original

```powershell
param (
    [string]$Url          = "https://example.com/tool.zip",
    [string]$Destination  = "C:\Tools\tool.zip",
    [string]$ExpectedHash = "abc123..."
)

Write-Host "Downloading $Url..."
Invoke-WebRequest -Uri $Url -OutFile $Destination

$actual = (Get-FileHash -Path $Destination -Algorithm SHA256).Hash.ToLower()

if ($actual -eq $ExpectedHash.ToLower()) {
    Write-Host "Checksum verified ✓"
} else {
    Write-Error "Checksum mismatch! Expected: $ExpectedHash Got: $actual"
    Remove-Item $Destination
    exit 1
}
```

**Notes:** `Invoke-WebRequest` for file downloads is the PS way — equivalent to
`curl`. `Get-FileHash` is built-in. The `.ToLower()` normalisation is a common
gotcha — hash comparison is case-sensitive by default.

-----

### Grob equivalent

```grob
import Grob.Http
import Grob.Crypto

param url:           string
param destination:   string
param expected_hash: string

log.info("Downloading ${url}...")
http.download(url, destination)

if (crypto.verifySha256(destination, expected_hash)) {
    log.info("Checksum verified ✓")
} else {
    actual := crypto.sha256File(destination)
    log.error("Checksum mismatch! Expected: ${expected_hash}  Got: ${actual}")
    fs.delete(destination)
    exit(1)
}
```

**What this demonstrates:**

- `Grob.Http` and `Grob.Crypto` as first-party plugins working together
- `http.download()` — streams file to disk, does not load into memory
- `crypto.verifySha256()` — constant-time comparison, names the intent
- `crypto.sha256File()` — lowercase hex output, streams file internally
- `fs.delete()` for file removal
- `exit(1)` to signal failure to calling process
- `log.info()` / `log.error()` for output

**Gaps noted:** None — `Grob.Crypto` API fully specified.

-----

## Script 11 — Azure Resource Provisioning Helper

**Use case:** DevOps / cloud engineer. Generate deterministic resource IDs for
idempotent Azure deployments, verify template integrity before deploying, call
Azure REST API. Real-world Bicep/ARM automation.

**Confidence:** ⭐⭐⭐

-----

### PowerShell equivalent (sketch)

```powershell
param (
    [Parameter(Mandatory)] [string]$SubscriptionId,
    [Parameter(Mandatory)] [string]$TenantId,
    [string]$ResourceGroup,
    [string]$Environment     = "dev",
    [string]$TemplatePath    = "main.bicep",
    [string]$ExpectedHash
)

# Verify template
$actual = (Get-FileHash -Path $TemplatePath -Algorithm SHA256).Hash.ToLower()
if ($actual -ne $ExpectedHash.ToLower()) {
    Write-Error "Template integrity check failed."
    exit 1
}

# PowerShell has no built-in deterministic GUID — requires .NET interop
$ns  = [guid]"6ba7b811-9dad-11d1-80b4-00c04fd430c8"
# ... complex .NET GuidV5 implementation omitted ...

$tags = @{ environment = $Environment; deployedBy = "grob" }

$token = $env:AZURE_TOKEN
$uri   = "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Resources/deployments/grob-deploy?api-version=2021-04-01"
$body  = @{ properties = @{ mode = "Incremental"; templateLink = @{ uri = $TemplatePath } } } | ConvertTo-Json -Depth 10
$headers = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }

$response = Invoke-RestMethod -Uri $uri -Method Put -Headers $headers -Body $body
```

**Notes:** PowerShell has no built-in deterministic GUID generation — requires complex
.NET interop for v5 GUIDs. The `ConvertTo-Json -Depth 10` is a common gotcha. The
`@{}` hashtable syntax for tags and body construction is verbose.

-----

### Grob equivalent

```grob
import Grob.Http
import Grob.Crypto

@secure
param subscriptionId: string

@secure
param tenantId:       string

param resourceGroup:  string
param environment:    string = "dev"
param templatePath:   string = "main.bicep"
param expectedHash:   string

// Verify template integrity before deploying
actual := crypto.sha256File(templatePath)
if (!crypto.verifySha256(templatePath, expectedHash)) {
    log.error("Template integrity check failed.")
    log.error("Expected: ${expectedHash}")
    log.error("Got:      ${actual}")
    exit(1)
}
log.info("Template verified ✓")

// Generate deterministic resource IDs — same inputs always produce same GUIDs
storageId  := guid.newV5(guid.namespaces.url, subscriptionId, resourceGroup, "storage",  environment)
functionId := guid.newV5(guid.namespaces.url, subscriptionId, resourceGroup, "function", environment)
cosmosId   := guid.newV5(guid.namespaces.url, subscriptionId, resourceGroup, "cosmos",   environment)

log.info("Resource IDs (deterministic):")
log.info("  storage:  ${storageId}")
log.info("  function: ${functionId}")
log.info("  cosmos:   ${cosmosId}")

// Build deployment tags as a map
tags := map<string, string>{
    "environment": environment
    "deployedBy":  "grob"
    "storageId":   storageId.toString()
    "functionId":  functionId.toString()
}

log.info("Deployment tags:")
for k, v in tags {
    log.info("  ${k}: ${v}")
}

// Deploy via Azure REST API
token  := env.require("AZURE_TOKEN")
url    := "https://management.azure.com/subscriptions/${subscriptionId}/resourceGroups/${resourceGroup}/providers/Microsoft.Resources/deployments/grob-deploy?api-version=2021-04-01"

body := #{
    properties: #{
        mode: "Incremental"
        templateLink: #{ uri: templatePath }
        parameters: #{
            environment: #{ value: environment }
            storageId:   #{ value: storageId.toString() }
        }
    }
    tags: tags
}

response := http.put(url, json.encode(body), auth.bearer(token))

if (response.isSuccess) {
    log.info("Deployment accepted. Status: ${response.statusCode}")
} else {
    log.error("Deployment failed. Status: ${response.statusCode}")
    log.error(response.asText())
    exit(1)
}
```

**What this demonstrates:**

- `guid.newV5()` for deterministic resource naming — same inputs, same GUIDs
- `guid.namespaces.url` well-known namespace constant
- `guid.toString()` for map key insertion (v1: string keys only)
- `Grob.Crypto` for template integrity verification — `sha256File()` and `verifySha256()`
- `map<string, string>` construction with literal syntax
- `for k, v in tags` — map iteration
- `Grob.Http` `http.put()` for Azure ARM REST API
- `json.encode()` to serialise anonymous struct body before `http.put()`
- `auth.bearer()` for token authentication
- `env.require()` for credential access
- `@secure` params for subscription and tenant IDs
- Anonymous struct nesting (`#{ }` inside `#{ }`) for JSON body construction
- `response.isSuccess` and `response.statusCode` on `Response` type

**Gaps noted:** None — this script uses only confirmed language features, stdlib and plugins.

-----

## Gap Summary

### Remaining gaps

|Gap                                            |Surfaces in|Priority                                                                                      |
|-----------------------------------------------|-----------|----------------------------------------------------------------------------------------------|
|`sys` / disk introspection module              |Script 9   |Low — workaround via `process.run()` exists                                                   |

### Resolved since original version

|Gap                                                                               |Resolution                                                                                   |
|----------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------|
|`exit(code)`                                                                      |Built-in function, no namespace. `exit()` = 0, `exit(1)` = failure. Uncatchable `ExitSignal`.|
|`fs.exists()`, `fs.ensureDir()`, `fs.delete()`                                    |Full `fs` module API specified — all confirmed                                               |
|`File` type — properties and methods                                              |Built-in type registered by fs plugin. Full registry in decisions log.                       |
|`.sort(fn, descending)` on arrays                                                 |Added to `T[]` registry                                                                      |
|`float.round(decimals)`                                                           |Added to `float` registry — `round() → int`, `round(decimals) → float`                       |
|`if/else if` as expression                                                        |Decided against. Use ternary `? :` or switch expression instead.                             |
|`csv.parse(text, delimiter)`                                                      |Confirmed — `csv.parse()` for in-memory strings                                              |
|Anonymous struct literals                                                         |Confirmed — `#{ field: value }` syntax, distinct from block `{ }`                            |
|Array indexing `arr[n]`                                                           |Confirmed — `[]` for indexing, `()` for calls, no ambiguity                                  |
|Named parameter calling convention                                                |Confirmed — positional first, named after, only defaulted params may be named                |
|Lambda closures                                                                   |Confirmed — upvalue mechanism follows clox design                                            |
|`Grob.Http` API shape — all methods including PATCH, `Response` type, `download()`|Full API specified in decisions log. Scripts 4, 7, 10 confirmed against it.                  |
|`Grob.Http.Auth` as separate plugin                                               |Retired. `auth.*` is a sub-namespace of `Grob.Http`. Single `import Grob.Http` suffices.     |
|`format` module calling convention                                                |`.format.table()` chained form confirmed. Compiler namespace rewrite — no runtime object.    |
|`date.daysUntil()` — interval between two dates                                   |`daysUntil(other: date) → int` and `daysSince(other: date) → int` added to `date` registry.  |
|`http.get(url, token).asJson()` chaining                                          |Confirmed — `Response.asJson()` returns `json.Node`.                                         |
|`http.download(url, path)`                                                        |Confirmed — streaming to disk, does not load into memory.                                    |
|`Grob.Crypto` API shape — `sha256File()` etc                                      |Full API specified — `sha256File`, `md5File`, `sha256`, `md5`, `verifySha256`, `verifyMd5`. All hex output lowercase.|

-----

## Assumptions That Need Confirming

|Assumption                                     |Used in  |Notes                                                                        |
|-----------------------------------------------|---------|-----------------------------------------------------------------------------|
|*(none outstanding)*                            |—        |—                                                                            |

### Confirmed since original version

|Assumption                                                     |Resolution                                                         |
|---------------------------------------------------------------|-------------------------------------------------------------------|
|`fs.list()` returns objects with `.name`, `.size`, `.directory`|`File` type fully specified in decisions log                       |
|`fs.list(path, recursive: true)` named param form              |Confirmed in `fs` module API                                       |
|`string.split(sep)` returns `string[]`                         |Confirmed in string type registry                                  |
|`process.run()` result `.stdout` is a string                   |`ProcessResult` type confirmed: `stdout`, `stderr`, `exitCode`     |
|Struct construction in lambdas                                 |Confirmed — named struct `Type { }` and anonymous `#{ }` both valid|
|`http.get(url, auth).asJson()` chaining                        |Confirmed — `Grob.Http` API fully specified                        |
|`http.download(url, path)` for file saves                      |Confirmed — streaming to disk                                      |
|Nested struct field access (`issue.user.login`)                |Confirmed — type checker resolves field chains against the registry; undeclared field at any level is a compile error. See decisions log.|
|`crypto.sha256File(path)`                                      |Confirmed — `Grob.Crypto` API fully specified. Lowercase hex output, streams internally.|

-----

*Last updated: April 2026 — pre-implementation review: Script 11 fixed*
*(`json.encode()` added before `http.put()` — body must be string per locked API);*
*Scripts 3, 5, 7, 8 fixed (dangling `.format.table()` wrapped in `print()`);*
*Script 4 `[ASSUMPTION]` tag on `fs.ensureDir()` cleared (confirmed);*
*Script 8 `[ASSUMPTION]` tag on `string.split` cleared (confirmed).*
*Previous: Script 10 updated with resolved `Grob.Crypto` API;*
*Script 11 (Azure Resource Provisioning Helper) added — exercises `guid.newV5()`,*
*`Grob.Crypto`, `map<K, V>` iteration, `Grob.Http`. `Grob.Crypto` gap resolved.*
*Previous: nested struct field access confirmed; Script 8 confidence corrected to ⭐⭐⭐.*
*Update this document after every design session that affects the API surface.*
*Remaining gaps are inputs to the next design session.*