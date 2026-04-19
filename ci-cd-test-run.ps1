<#
.SYNOPSIS
    Local CI/CD test runner for Blazing.Extensions.Http.

.DESCRIPTION
    Validates and executes GitHub Actions workflows locally using actionlint (static analysis)
    and act (Docker-based execution). Mirrors GitHub Actions behavior for net8.0/net9.0/net10.0.

.PARAMETER Mode
    dry      - Validate workflow graph via act dry-run only (requires Docker + act)
    lint     - actionlint static analysis only
    ci       - Full workflow execution via act
    all      - lint + ci (default)

.PARAMETER Workflow
    ci       - Run only ci.yml (default)
    release  - Run only release.yml
    both     - Run both workflows

.PARAMETER Job
    Optionally run one job by name (for Mode=ci).

.EXAMPLE
    .\ci-cd-test-run.ps1
    .\ci-cd-test-run.ps1 -Mode lint
    .\ci-cd-test-run.ps1 -Mode dry
    .\ci-cd-test-run.ps1 -Mode dry -Workflow both
    .\ci-cd-test-run.ps1 -Mode ci -Workflow release
    .\ci-cd-test-run.ps1 -Mode ci -Job build-and-test
#>

[CmdletBinding()]
param(
    [ValidateSet('dry', 'lint', 'ci', 'all')]
    [string]$Mode = 'all',

    [ValidateSet('ci', 'release', 'both')]
    [string]$Workflow = 'ci',

    [string]$Job = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Header  { param([string]$msg) Write-Host "`n━━━ $msg ━━━" -ForegroundColor Cyan }
function Write-Pass    { param([string]$msg) Write-Host "  ✅ $msg" -ForegroundColor Green }
function Write-Fail    { param([string]$msg) Write-Host "  ❌ $msg" -ForegroundColor Red }
function Write-Warn    { param([string]$msg) Write-Host "  ⚠  $msg" -ForegroundColor Yellow }
function Write-Section { param([string]$msg) Write-Host "`n  ▶ $msg" -ForegroundColor White }

$Script:Errors   = [System.Collections.Generic.List[string]]::new()
$Script:Warnings = [System.Collections.Generic.List[string]]::new()

function Add-Error   { param([string]$msg) $Script:Errors.Add($msg);   Write-Fail $msg }
function Add-Warning { param([string]$msg) $Script:Warnings.Add($msg); Write-Warn $msg }

function Test-IsWindows {
    return [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
}

function Test-IsMacOS {
    return [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)
}

$RepoRoot     = $PSScriptRoot
$WorkflowDir  = Join-Path $RepoRoot '.github' 'workflows'
$CiYaml       = Join-Path $WorkflowDir 'ci.yml'
$ReleaseYaml  = Join-Path $WorkflowDir 'release.yml'
$CiSlnf       = Join-Path $RepoRoot 'Blazing.Extensions.Http.CI.slnf'

function Test-Tool {
    param([string]$Name)
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

# ─────────────────────────────────────────────────────────────────────────────
Write-Header 'Prerequisite Check'

# dotnet: optional for all modes handled by this script.
# - lint uses actionlint only and does not require a host .NET SDK.
# - act-backed modes install .NET inside the runner via actions/setup-dotnet.
if (Test-Tool 'dotnet') {
    Write-Pass "dotnet $(dotnet --version)"
} else {
    Add-Warning "Tool 'dotnet' not found on host. Continuing because this script's lint mode uses actionlint only, and act-backed workflows install .NET inside the runner via actions/setup-dotnet."
}

if (-not (Test-Path $CiYaml))     { Add-Error "Missing workflow file: $CiYaml" }
if (-not (Test-Path $ReleaseYaml)) { Add-Error "Missing workflow file: $ReleaseYaml" }
if (-not (Test-Path $CiSlnf))     { Add-Error "Missing CI solution filter: $CiSlnf" }

$needsActionlint = $Mode -in @('lint', 'all')
$needsAct        = $Mode -in @('dry', 'ci', 'all')

$hasActionlint = $false
if ($needsActionlint) {
    $hasActionlint = Test-Tool 'actionlint'
    if (-not $hasActionlint) {
        Add-Error "Tool 'actionlint' not found. Install: winget install rhysd.actionlint"
    } else {
        Write-Pass 'actionlint found'
    }
}

$hasAct         = $false
$dockerAvailable = $false
if ($needsAct) {
    $hasAct = Test-Tool 'act'
    if (-not $hasAct) {
        Add-Error "Tool 'act' not found. Install: winget install nektos.act"
    } else {
        Write-Pass 'act found'
    }

    # Check Docker binary first — docker info throws unhelpfully if not installed
    $hasDocker = Test-Tool 'docker'
    if (-not $hasDocker) {
        $installHint = if (Test-IsWindows) {
            'Install Docker Desktop: https://docs.docker.com/desktop/setup/install/windows-install/'
        } elseif (Test-IsMacOS) {
            'brew install --cask docker'
        } else {
            'Install Docker Engine: https://docs.docker.com/engine/install/'
        }
        Add-Error "Tool 'docker' not found. $installHint"
    } else {
        $null = & docker info *> $null
        if ($LASTEXITCODE -eq 0) {
            $dockerAvailable = $true
            Write-Pass 'Docker daemon reachable'
        } else {
            Add-Error 'Docker not reachable — act dry/ci modes require Docker daemon running'
        }
    }
}

# Bail early if any hard errors found in prerequisites
if ($Script:Errors.Count -gt 0) {
    Write-Header 'Summary'
    Write-Host "`n  ❌ $($Script:Errors.Count) error(s) found:" -ForegroundColor Red
    $Script:Errors | ForEach-Object { Write-Host "    • $_" -ForegroundColor Red }
    Write-Host ''
    exit 1
}

# ─────────────────────────────────────────────────────────────────────────────
if ($Mode -in @('lint', 'all') -and $hasActionlint) {
    Write-Header 'YAML Static Analysis (actionlint)'

    $yamlFiles = @()
    if ($Workflow -in @('ci', 'both'))      { $yamlFiles += $CiYaml }
    if ($Workflow -in @('release', 'both')) { $yamlFiles += $ReleaseYaml }

    foreach ($yaml in $yamlFiles) {
        $name = Split-Path $yaml -Leaf
        Write-Section "Linting $name"
        $out = actionlint $yaml 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Pass "$name — no issues"
        } else {
            $out | ForEach-Object { Write-Host "    $_" -ForegroundColor Yellow }
            Add-Error "$name has actionlint violations (see above)"
        }
    }
}

# ─────────────────────────────────────────────────────────────────────────────
if ($Mode -in @('dry', 'all') -and $hasAct -and $dockerAvailable) {
    Write-Header 'Workflow Dry-Run (act -n)'

    $dryWorkflows = @()
    if ($Workflow -in @('ci', 'both'))      { $dryWorkflows += @{ Name = 'CI';      File = $CiYaml } }
    if ($Workflow -in @('release', 'both')) { $dryWorkflows += @{ Name = 'Release'; File = $ReleaseYaml } }

    foreach ($wf in $dryWorkflows) {
        Write-Section "Dry-run $($wf.Name) workflow"
        Push-Location $RepoRoot
        $eventPath = $null
        try {
            $actArgs = @('push', '--workflows', $wf.File, '--env', 'RUNNING_LOCALLY=true', '-n')

            # Release workflow is gated on push to master. Without an explicit push event
            # payload, act uses its own default ref which does not match the branch filter
            # → workflow silently skipped. Provide the payload for both dry-run and full-exec.
            if ($wf.Name -eq 'Release') {
                $eventPath = [System.IO.Path]::GetTempFileName()
                @{
                    ref        = 'refs/heads/master'
                    repository = @{ default_branch = 'master' }
                    head_commit = @{ id = 'local-dry-run' }
                } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $eventPath -Encoding UTF8
                $actArgs += @('-e', $eventPath)
            }

            $out = & act @actArgs 2>&1

            # Narrow filter — only match the known act Windows artifact-cache cleanup bug.
            # Never use broad strings like 'The system cannot find the file specified' which
            # can come from any failure and would mask real errors.
            $failed = @($out | Where-Object {
                $_ -match '(FAIL|error)' -and
                $_ -notmatch 'DRYRUN' -and
                $_ -notmatch 'upload-artifact' -and
                $_ -notmatch '\.cache\\act\\actions-upload-artifact'
            })
            $knownArtifactCacheIssue = @($out | Where-Object {
                $_ -match '\.cache\\act\\actions-upload-artifact'
            })

            if ($knownArtifactCacheIssue.Count -gt 0) {
                Add-Warning "$($wf.Name) dry-run: known act artifact-cache cleanup noise detected (harmless)."
            }

            # Detect when no dry-run jobs were staged (workflow may have been silently skipped)
            $dryRunLines = @($out | Where-Object { $_ -match '\*DRYRUN\* \[[^\]]+\]' })
            if ($dryRunLines.Count -eq 0) {
                Add-Warning "$($wf.Name) dry-run: no jobs were staged — workflow may have been skipped. Verify trigger ref and branch filter."
            } elseif ($LASTEXITCODE -eq 0 -and $failed.Count -eq 0) {
                Write-Pass "$($wf.Name) dry-run succeeded"
            } else {
                $out | Where-Object { $_ -match '(FAIL|error|warn)' } | ForEach-Object { Write-Host "    $_" -ForegroundColor Yellow }
                Add-Error "$($wf.Name) dry-run reported issues (exit $LASTEXITCODE)"
            }
        } finally {
            if ($null -ne $eventPath -and (Test-Path -LiteralPath $eventPath)) {
                Remove-Item -LiteralPath $eventPath -Force -ErrorAction SilentlyContinue
            }
            Pop-Location
        }
    }
}

# ─────────────────────────────────────────────────────────────────────────────
if ($Mode -in @('ci', 'all') -and $hasAct -and $dockerAvailable) {
    Write-Header 'Full CI Execution (act)'

    $actWorkflows = @()
    if ($Workflow -in @('ci', 'both'))      { $actWorkflows += @{ Name = 'CI';      File = $CiYaml;      Event = 'push' } }
    if ($Workflow -in @('release', 'both')) { $actWorkflows += @{ Name = 'Release'; File = $ReleaseYaml; Event = 'push' } }

    foreach ($wf in $actWorkflows) {
        Write-Section "Running $($wf.Name) workflow via act"
        Push-Location $RepoRoot
        $eventPath = $null
        try {
            $actArgs = @($wf.Event, '--workflows', $wf.File, '--env', 'RUNNING_LOCALLY=true')
            if ($Job) { $actArgs += @('-j', $Job) }

            # Release workflow requires an explicit event payload so act matches the branch filter.
            if ($wf.Name -eq 'Release') {
                $eventPath = [System.IO.Path]::GetTempFileName()
                @{
                    ref        = 'refs/heads/master'
                    repository = @{ default_branch = 'master' }
                    head_commit = @{ id = 'local-ci-run' }
                } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $eventPath -Encoding UTF8
                $actArgs += @('-e', $eventPath)
            }

            $outLines = [System.Collections.Generic.List[string]]::new()
            & act @actArgs 2>&1 | ForEach-Object {
                $outLines.Add($_)
                if ($_ -match '(✅|❌|🏁|PASS|FAIL|Error|error:|warning:)') {
                    Write-Host "    $_"
                }
            }

            # Wrap all Where-Object results in @() to ensure .Count is always safe
            # (Where-Object returns a single object, not an array, when exactly one item matches)
            $jobSucceeded = @($outLines | Where-Object { $_ -match '🏁.*Job succeeded' })
            $jobFailed    = @($outLines | Where-Object { $_ -match '🏁.*Job failed' })
            $testPassed   = @($outLines | Where-Object { $_ -match 'Passed!.*Failed:\s+0' })
            $testFailed   = @($outLines | Where-Object { $_ -match 'Failed!.*Failed:\s+[^0]' })

            if ($testPassed.Count -gt 0) {
                $testPassed | ForEach-Object { Write-Pass ($_ -replace '^\|\s*', '') }
            }
            if ($testFailed.Count -gt 0) {
                $testFailed | ForEach-Object { Add-Error ($_ -replace '^\|\s*', '') }
            }

            $realFailures = @($jobFailed | Where-Object { $_ -notmatch 'Upload test results' })

            if ($LASTEXITCODE -eq 0 -or ($jobSucceeded.Count -gt 0 -and $realFailures.Count -eq 0)) {
                Write-Pass "$($wf.Name) workflow — all jobs succeeded"
            } else {
                Add-Error "$($wf.Name) workflow had job failures (see above)"
            }
        } finally {
            if ($null -ne $eventPath -and (Test-Path -LiteralPath $eventPath)) {
                Remove-Item -LiteralPath $eventPath -Force -ErrorAction SilentlyContinue
            }
            Pop-Location
        }
    }
}

# ─────────────────────────────────────────────────────────────────────────────
Write-Header 'Summary'

if ($Script:Warnings.Count -gt 0) {
    Write-Host "`n  Warnings:" -ForegroundColor Yellow
    $Script:Warnings | ForEach-Object { Write-Host "    ⚠  $_" -ForegroundColor Yellow }
}

if ($Script:Errors.Count -eq 0) {
    Write-Host "`n  ✅ All checks passed!`n" -ForegroundColor Green
    exit 0
}

Write-Host "`n  ❌ $($Script:Errors.Count) error(s) found:" -ForegroundColor Red
$Script:Errors | ForEach-Object { Write-Host "    • $_" -ForegroundColor Red }
Write-Host ''
exit 1
