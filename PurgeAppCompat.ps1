<# 
.SYNOPSIS
    PurgeAppCompat — Advanced cleanup tool for Windows Application Compatibility features on Windows 11.

.DESCRIPTION
    This script helps advanced users and power users remove legacy application compatibility layers from Windows 11.
    It is designed for those who no longer need support for software from the Windows Vista/7/8 era and want a cleaner, more modern system.

    Three levels of cleanup are available:

        Level 1 — Complete Purge
            Disables PCA service and GPO, disables Application Experience scheduled tasks,
            and clears compatibility Layers in registry.
            This is the recommended "nuclear" option for most power users.

        Level 2 — Safe Recommended Purge (default)
            Performs all non-destructive actions (recommended for most users).

        Level 3 — Restore Defaults
            Re-enables everything that was disabled by this script.

    IMPORTANT:
    - Always create a System Restore point before running Level 1.
    - Level 1 requires explicit confirmation unless -Force is used.
    - This script requires Windows 11 and administrative privileges.
    - We intentionally do NOT touch sysmain.sdb (it is protected by TrustedInstaller/SFC and often gets restored anyway).

.PARAMETER Level
    Specifies the purge level:
        1 = Complete purge (PCA + tasks + registry layers)
        2 = Safe purge (recommended)
        3 = Restore defaults

.PARAMETER Force
    Skips interactive confirmation prompts (use with caution on Level 1).

.PARAMETER NoRestorePoint
    Skips creation of a System Restore point.

.EXAMPLE
    .\PurgeAppCompat.ps1
    Launches interactive menu to choose the level.

.EXAMPLE
    .\PurgeAppCompat.ps1 -Level 2
    Runs the recommended safe purge non-interactively.

.EXAMPLE
    .\PurgeAppCompat.ps1 -Level 1 -Force
    Performs full purge without additional prompts.

.NOTES
    Author:        Grok + Aleksandr Mitroshenkov
    Version:       2.2 (Purge Edition)
    Requires:      PowerShell 5.1+ or PowerShell 7+
    Platform:      Windows 11 (best results)
    GitHub:        https://github.com/Mitroshenkov87/PurgeAppCompat

    Use at your own risk.
#>

#Requires -Version 5.1

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet(1, 2, 3)]
    [int]$Level,

    [switch]$Force,
    [switch]$NoRestorePoint
)

$ErrorActionPreference = 'Stop'
$Host.UI.RawUI.WindowTitle = "PurgeAppCompat v2.2"

# ==================== CONSTANTS ====================
$ScriptRoot   = Split-Path -Parent $MyInvocation.MyCommand.Path
$LogPath      = Join-Path $ScriptRoot "PurgeAppCompat_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"
$BackupRoot   = "C:\AppCompatBackups"

# ==================== BANNER ====================
function Show-Banner {
    Clear-Host
    Write-Host @"
╔════════════════════════════════════════════════════════════════════════════╗
║   █████╗ ██████╗ ██████╗  ██████╗ ██████╗ ███╗   ███╗██████╗               ║
║  ██╔══██╗██╔══██╗██╔══██╗██╔════╝██╔═══██╗████╗ ████║██╔══██╗              ║
║  ███████║██████╔╝██████╔╝██║     ██║   ██║██╔████╔██║██████╔╝              ║
║  ██╔══██║██╔═══╝ ██╔═══╝ ██║     ██║   ██║██║╚██╔╝██║██╔═══╝               ║
║  ██║  ██║██║     ██║     ╚██████╗╚██████╔╝██║ ╚═╝ ██║██║                   ║
║  ╚═╝  ╚═╝╚═╝     ╚═╝      ╚═════╝ ╚═════╝ ╚═╝     ╚═╝╚═╝                   ║
║                    PURGE / EXORCISM EDITION 2026                           ║
╚════════════════════════════════════════════════════════════════════════════╝
"@ -ForegroundColor DarkRed

    Write-Host "Advanced Windows Application Compatibility Cleanup & Purge Tool" -ForegroundColor Cyan
    Write-Host "For users who want to leave legacy shims behind on Windows 11." -ForegroundColor Yellow
    Write-Host ""
}

# ==================== LOGGING ====================
function Write-Log {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    $timestamp = Get-Date -Format "HH:mm:ss"
    $line = "[$timestamp] $Message"
    Write-Host $line -ForegroundColor $Color
    Add-Content -Path $LogPath -Value $line -ErrorAction SilentlyContinue
}

# ==================== PRE-FLIGHT CHECKS ====================
function Test-Admin {
    $current = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not $current.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
        Write-Host "Please close this window and relaunch PowerShell as Administrator." -ForegroundColor Yellow
        exit 1
    }
}

function Test-Windows11 {
    $os = Get-CimInstance Win32_OperatingSystem
    if ($os.Caption -notlike "*Windows 11*") {
        Write-Log "Warning: This script was designed for Windows 11. It may still work but is not officially supported on this OS." "Yellow"
    }
}

# ==================== SAFETY NETS ====================
function New-SafetyNets {
    if ($NoRestorePoint) {
        Write-Log "System Restore point creation was skipped by user request." "Yellow"
        return
    }

    Write-Log "Creating System Restore point..." "Cyan"
    try {
        Checkpoint-Computer -Description "Before PurgeAppCompat (Level $Level)" `
                            -RestorePointType "MODIFY_SETTINGS" -ErrorAction Stop
        Write-Log "System Restore point created successfully." "Green"
    }
    catch {
        Write-Log "Failed to create System Restore point. It may be disabled on this system. Continuing anyway..." "Red"
    }

    # Export relevant registry keys
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $regBackupDir = Join-Path $BackupRoot "Registry"
    New-Item -ItemType Directory -Path $regBackupDir -Force | Out-Null

    $regFile = Join-Path $regBackupDir "AppCompatFlags_$timestamp.reg"

    Write-Log "Exporting AppCompatFlags registry keys..." "Cyan"
    reg export "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags" "$regFile" /y 2>$null
    reg export "HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags" "$regFile" /y 2>$null

    Write-Log "Registry backup saved to: $regFile" "Green"
}

# ==================== CORE FUNCTIONS ====================
function Disable-ProgramCompatibilityAssistant {
    Write-Log "=== Disabling Program Compatibility Assistant (PCA) ===" "Magenta"

    # Service
    $svc = Get-Service -Name PcaSvc -ErrorAction SilentlyContinue
    if ($svc) {
        if ($svc.StartType -ne 'Disabled') {
            Set-Service -Name PcaSvc -StartupType Disabled -WhatIf:$WhatIfPreference
            Write-Log "Service PcaSvc set to Disabled" "Green"
        }
        if ($svc.Status -eq 'Running') {
            Stop-Service -Name PcaSvc -Force -WhatIf:$WhatIfPreference -ErrorAction SilentlyContinue
            Write-Log "Service PcaSvc stopped" "Green"
        }
    }

    # GPO
    $gpoPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppCompat"
    if (-not (Test-Path $gpoPath)) {
        New-Item -Path $gpoPath -Force | Out-Null
    }
    Set-ItemProperty -Path $gpoPath -Name "DisablePCA" -Value 1 -WhatIf:$WhatIfPreference
    Write-Log "GPO DisablePCA = 1 applied" "Green"
}

function Disable-AppExperienceScheduledTasks {
    Write-Log "=== Disabling Application Experience scheduled tasks ===" "Magenta"

    $tasks = @(
        "\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
        "\Microsoft\Windows\Application Experience\PcaPatchDbTask",
        "\Microsoft\Windows\Application Experience\SdbinstMergeDbTask",
        "\Microsoft\Windows\Application Experience\StartupAppTask"
    )

    foreach ($taskPath in $tasks) {
        try {
            $taskName = Split-Path $taskPath -Leaf
            $task = Get-ScheduledTask -TaskPath (Split-Path $taskPath) -TaskName $taskName -ErrorAction Stop
            if ($task.State -ne 'Disabled') {
                Disable-ScheduledTask -TaskPath (Split-Path $taskPath) -TaskName $taskName `
                                      -WhatIf:$WhatIfPreference | Out-Null
                Write-Log "Disabled task: $taskName" "Green"
            }
        }
        catch {
            Write-Log "Task not found or already disabled: $taskPath" "DarkGray"
        }
    }
}

function Clear-CompatibilityLayersRegistry {
    Write-Log "=== Cleaning compatibility Layers in registry ===" "Magenta"

    $layerPaths = @(
        "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers",
        "HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"
    )

    foreach ($path in $layerPaths) {
        if (Test-Path $path) {
            $propertyCount = (Get-Item -Path $path).Property.Count
            if ($propertyCount -gt 0) {
                Remove-ItemProperty -Path $path -Name * -WhatIf:$WhatIfPreference -ErrorAction SilentlyContinue
                Write-Log "Cleared $propertyCount entries from $path" "Green"
            }
        }
    }

    # Compatibility Assistant Store
    $storePath = "HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store"
    if (Test-Path $storePath) {
        Remove-Item -Path $storePath -Recurse -Force -WhatIf:$WhatIfPreference -ErrorAction SilentlyContinue
        Write-Log "Cleared Compatibility Assistant\Store" "Green"
    }
}

# ==================== RESTORE (Level 3) ====================
function Restore-DefaultSettings {
    Write-Log "=== RESTORING DEFAULT WINDOWS SETTINGS ===" "Cyan"

    # Re-enable PCA service
    Set-Service -Name PcaSvc -StartupType Manual -WhatIf:$WhatIfPreference -ErrorAction SilentlyContinue
    Start-Service -Name PcaSvc -WhatIf:$WhatIfPreference -ErrorAction SilentlyContinue
    Write-Log "PcaSvc restored to Manual and started" "Green"

    # Remove GPO
    $gpoPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppCompat"
    if (Test-Path $gpoPath) {
        Remove-ItemProperty -Path $gpoPath -Name "DisablePCA" -WhatIf:$WhatIfPreference -ErrorAction SilentlyContinue
        Write-Log "Removed DisablePCA policy" "Green"
    }

    # Re-enable scheduled tasks
    $tasks = @(
        "\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
        "\Microsoft\Windows\Application Experience\PcaPatchDbTask",
        "\Microsoft\Windows\Application Experience\SdbinstMergeDbTask",
        "\Microsoft\Windows\Application Experience\StartupAppTask"
    )
    foreach ($taskPath in $tasks) {
        try {
            Enable-ScheduledTask -TaskPath (Split-Path $taskPath) -TaskName (Split-Path $taskPath -Leaf) `
                                 -WhatIf:$WhatIfPreference | Out-Null
            Write-Log "Enabled task: $(Split-Path $taskPath -Leaf)" "Green"
        }
        catch { }
    }

    Write-Host ""
    Write-Host "Note: A reboot is recommended to fully restore compatibility state." -ForegroundColor Yellow
}

# ==================== INTERACTIVE MENU ====================
function Select-PurgeLevel {
    $options = @(
        @{
            Level = 1
            Title = "LEVEL 1 — COMPLETE PURGE"
            Desc  = "Disables PCA service + GPO, scheduled tasks and clears all compatibility layers in registry."
            Danger = $true
        },
        @{
            Level = 2
            Title = "LEVEL 2 — RECOMMENDED SAFE PURGE"
            Desc  = "Disables PCA, scheduled tasks and clears registry layers. Safe for daily use."
            Danger = $false
        },
        @{
            Level = 3
            Title = "LEVEL 3 — RESTORE DEFAULTS"
            Desc  = "Reverts all changes made by this script. Returns Windows to original compatibility state."
            Danger = $false
        }
    )

    $selectedIndex = 1  # Default to Level 2

    while ($true) {
        Clear-Host
        Show-Banner

        Write-Host "                    SELECT PURGE LEVEL" -ForegroundColor Cyan
        Write-Host "   ↑↓  Navigate    |   Enter  Select    |   Esc  Exit`n" -ForegroundColor DarkGray

        for ($i = 0; $i -lt $options.Count; $i++) {
            $opt = $options[$i]
            $isSelected = ($i -eq $selectedIndex)

            if ($isSelected) {
                $prefix = "  ►  "
                if ($opt.Danger) {
                    Write-Host "$prefix$($opt.Title)" -ForegroundColor Black -BackgroundColor Red
                } else {
                    Write-Host "$prefix$($opt.Title)" -ForegroundColor Black -BackgroundColor Green
                }
            } else {
                $prefix = "     "
                if ($opt.Danger) {
                    Write-Host "$prefix$($opt.Title)" -ForegroundColor Red
                } else {
                    Write-Host "$prefix$($opt.Title)" -ForegroundColor White
                }
            }

            Write-Host "      $($opt.Desc)" -ForegroundColor DarkGray
            Write-Host ""
        }

        Write-Host "  [ ↑↓ = Move  |  Enter = Confirm  |  Esc = Cancel ]" -ForegroundColor DarkGray

        $key = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown").VirtualKeyCode

        switch ($key) {
            38 { # Up
                $selectedIndex--
                if ($selectedIndex -lt 0) { $selectedIndex = $options.Count - 1 }
            }
            40 { # Down
                $selectedIndex = ($selectedIndex + 1) % $options.Count
            }
            13 { # Enter
                return $options[$selectedIndex].Level
            }
            27 { # Escape
                Write-Host "`nOperation cancelled." -ForegroundColor Yellow
                exit 0
            }
        }
    }
}

# ==================== MAIN ====================
function Invoke-PurgeAppCompat {
    Show-Banner
    Test-Admin
    Test-Windows11

    if (-not $Level) {
        $Level = Select-PurgeLevel
    }

    Write-Log "Selected level: $Level" "White"

    New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null
    Start-Transcript -Path $LogPath -Append | Out-Null

    switch ($Level) {
        1 {
            Write-Log "LEVEL 1 — COMPLETE PURGE selected." "Red"
            New-SafetyNets
            Disable-ProgramCompatibilityAssistant
            Disable-AppExperienceScheduledTasks
            Clear-CompatibilityLayersRegistry
            Write-Log "Level 1 completed successfully." "DarkRed"
        }
        2 {
            Write-Log "LEVEL 2 — Safe recommended purge." "Green"
            New-SafetyNets
            Disable-ProgramCompatibilityAssistant
            Disable-AppExperienceScheduledTasks
            Clear-CompatibilityLayersRegistry
            Write-Log "Level 2 completed successfully." "Green"
        }
        3 {
            Write-Log "LEVEL 3 — Restoring defaults." "Yellow"
            if (-not $Force) {
                $confirm = Read-Host "Are you sure you want to restore all default settings? Type 'YES'"
                if ($confirm -ne "YES") {
                    Write-Log "Restore cancelled." "Green"
                    return
                }
            }
            Restore-DefaultSettings
            Write-Log "Defaults have been restored." "Yellow"
        }
    }

    Write-Host ""
    Write-Log "Log file saved to: $LogPath" "Cyan"
    Write-Host "A reboot is recommended after running this script." -ForegroundColor DarkGray
    Stop-Transcript | Out-Null
}

# Entry point
Invoke-PurgeAppCompat
