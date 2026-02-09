# Lefthook State Module (PowerShell)
#
# File-based state system for tracking hook execution progress.
# State is stored in .git/lefthook-state/hook-state.json.
#
# Consumers: PowerShell automation, Node.js hook scripts, C# Unity editor.
#
# Usage:
#   . ./LefthookState.ps1
#   Start-LefthookHook -HookName "pre-commit" -StepNames @("debug_logging", "run_tests")
#   Start-LefthookStep -StepName "debug_logging"
#   Complete-LefthookStep -StepName "debug_logging" -Success $true
#   Complete-LefthookHook -Status "passed"

$script:StateFileName = "hook-state.json"
$script:StateDirName = "lefthook-state"

function Get-LefthookGitDir {
    <#
    .SYNOPSIS
        Find the .git directory by walking up from the current location.
    #>
    $dir = Get-Location
    for ($i = 0; $i -lt 10; $i++) {
        $gitPath = Join-Path $dir ".git"
        if (Test-Path $gitPath) {
            $item = Get-Item $gitPath -Force
            if ($item.PSIsContainer) {
                return $gitPath
            }
            # .git file (worktrees) - read the gitdir pointer
            $content = (Get-Content $gitPath -Raw).Trim()
            if ($content -match '^gitdir:\s*(.+)$') {
                $resolved = [System.IO.Path]::GetFullPath((Join-Path $dir $Matches[1]))
                if (Test-Path $resolved) {
                    return $resolved
                }
            }
        }
        $parent = Split-Path $dir -Parent
        if (-not $parent -or $parent -eq $dir) { break }
        $dir = $parent
    }
    return $null
}

function Get-LefthookStateDir {
    <#
    .SYNOPSIS
        Get the state directory path (.git/lefthook-state/), creating it if needed.
    #>
    $gitDir = Get-LefthookGitDir
    if (-not $gitDir) { return $null }

    $stateDir = Join-Path $gitDir $script:StateDirName
    if (-not (Test-Path $stateDir)) {
        New-Item -ItemType Directory -Path $stateDir -Force | Out-Null
    }
    return $stateDir
}

function Get-LefthookStatePath {
    <#
    .SYNOPSIS
        Get the full path to hook-state.json.
    #>
    $stateDir = Get-LefthookStateDir
    if (-not $stateDir) { return $null }
    return Join-Path $stateDir $script:StateFileName
}

function Read-LefthookState {
    <#
    .SYNOPSIS
        Read the current hook state. Returns $null if missing or corrupt.
    #>
    $statePath = Get-LefthookStatePath
    if (-not $statePath -or -not (Test-Path $statePath)) { return $null }

    try {
        $content = Get-Content $statePath -Raw -Encoding UTF8
        return $content | ConvertFrom-Json
    } catch {
        Write-Warning "[lefthook-state] Failed to read state: $($_.Exception.Message)"
        return $null
    }
}

function Write-LefthookState {
    <#
    .SYNOPSIS
        Write state atomically (write to .tmp then rename).
    #>
    param (
        [Parameter(Mandatory)][object]$State
    )

    $statePath = Get-LefthookStatePath
    if (-not $statePath) {
        Write-Warning "[lefthook-state] Cannot write state: .git directory not found"
        return $false
    }

    $tmpPath = "$statePath.tmp"
    try {
        $State | ConvertTo-Json -Depth 10 | Set-Content $tmpPath -Force -Encoding UTF8
        Move-Item $tmpPath $statePath -Force
        return $true
    } catch {
        Write-Warning "[lefthook-state] Failed to write state: $($_.Exception.Message)"
        try { Remove-Item $tmpPath -Force -ErrorAction SilentlyContinue } catch {}
        return $false
    }
}

function Start-LefthookHook {
    <#
    .SYNOPSIS
        Initialize hook state with all steps set to "pending".
    #>
    param (
        [Parameter(Mandatory)][string]$HookName,
        [Parameter(Mandatory)][string[]]$StepNames
    )

    $steps = @()
    foreach ($name in $StepNames) {
        $steps += @{
            name = $name
            status = "pending"
            startTime = $null
            endTime = $null
            detail = $null
        }
    }

    $state = @{
        hookName = $HookName
        status = "running"
        startTime = (Get-Date).ToUniversalTime().ToString("o")
        endTime = $null
        pid = $PID
        machineName = $env:COMPUTERNAME
        steps = $steps
        result = $null
        error = $null
        testSummary = $null
    }

    Write-LefthookState -State $state
    return $state
}

function Update-LefthookStep {
    <#
    .SYNOPSIS
        Update a specific step's properties.
    #>
    param (
        [Parameter(Mandatory)][string]$StepName,
        [hashtable]$Updates
    )

    $state = Read-LefthookState
    if (-not $state) { return $null }

    $step = $state.steps | Where-Object { $_.name -eq $StepName } | Select-Object -First 1
    if (-not $step) {
        Write-Warning "[lefthook-state] Step not found: $StepName"
        return $state
    }

    foreach ($key in $Updates.Keys) {
        $step | Add-Member -NotePropertyName $key -NotePropertyValue $Updates[$key] -Force
    }

    Write-LefthookState -State $state
    return $state
}

function Start-LefthookStep {
    <#
    .SYNOPSIS
        Mark a step as "running".
    #>
    param (
        [Parameter(Mandatory)][string]$StepName,
        [string]$Detail
    )

    $updates = @{
        status = "running"
        startTime = (Get-Date).ToUniversalTime().ToString("o")
    }
    if ($Detail) {
        $updates.detail = $Detail
    }
    return Update-LefthookStep -StepName $StepName -Updates $updates
}

function Complete-LefthookStep {
    <#
    .SYNOPSIS
        Mark a step as "completed" or "failed".
    #>
    param (
        [Parameter(Mandatory)][string]$StepName,
        [bool]$Success = $true,
        [string]$Detail
    )

    $updates = @{
        status = if ($Success) { "completed" } else { "failed" }
        endTime = (Get-Date).ToUniversalTime().ToString("o")
    }
    if ($Detail) {
        $updates.detail = $Detail
    }
    return Update-LefthookStep -StepName $StepName -Updates $updates
}

function Skip-LefthookStep {
    <#
    .SYNOPSIS
        Mark a step as "skipped".
    #>
    param (
        [Parameter(Mandatory)][string]$StepName,
        [string]$Detail
    )

    $updates = @{ status = "skipped" }
    if ($Detail) {
        $updates.detail = $Detail
    }
    return Update-LefthookStep -StepName $StepName -Updates $updates
}

function Complete-LefthookHook {
    <#
    .SYNOPSIS
        Finalize the hook execution.
    #>
    param (
        [Parameter(Mandatory)][ValidateSet("passed", "failed", "error")][string]$Status,
        [string]$Error,
        [hashtable]$TestSummary
    )

    $state = Read-LefthookState
    if (-not $state) { return $null }

    $state | Add-Member -NotePropertyName "status" -NotePropertyValue $Status -Force
    $state | Add-Member -NotePropertyName "endTime" -NotePropertyValue ((Get-Date).ToUniversalTime().ToString("o")) -Force
    $state | Add-Member -NotePropertyName "error" -NotePropertyValue $Error -Force
    $state | Add-Member -NotePropertyName "testSummary" -NotePropertyValue $TestSummary -Force

    Write-LefthookState -State $state
    return $state
}
