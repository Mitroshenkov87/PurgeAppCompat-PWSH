using System;
using System.IO;

namespace PurgeAppCompat;

/// <summary>
/// Centralized constants for paths, registry keys, task names, and policy values.
/// Improves maintainability and reduces magic strings.
/// </summary>
public static class AppCompatConstants
{
    // Core paths
    public static readonly string WindowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    public static readonly string DefaultBackupRoot = @"C:\AppCompatBackups";
    public static readonly string PreferredBackupRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "PurgeAppCompat", "Backups");

    // Registry paths (HKLM)
    public const string AppCompatPolicyKey = @"SOFTWARE\Policies\Microsoft\Windows\AppCompat";
    public const string LayersHklmKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
    public const string LayersHkcuKey = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

    // Additional registry paths from original PowerShell / best practices (Level 1/2 enhancements)
    public const string CompatMarkersKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\CompatMarkers";
    public const string SharedKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Shared";
    public const string TargetVersionUpgradeKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\TargetVersionUpgradeExperienceIndicators";
    public const string TelemetryControllerKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\TelemetryController";
    public const string AppCompatCacheKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\AppCompatCache";

    // Services
    public const string PcaSvcName = "PcaSvc";

    // Scheduled Tasks (Application Experience)
    public static readonly string[] AppExperienceTasks =
    {
        @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
        @"\Microsoft\Windows\Application Experience\PcaPatchDbTask",
        @"\Microsoft\Windows\Application Experience\SdbinstMergeDbTask",
        @"\Microsoft\Windows\Application Experience\StartupAppTask"
    };

    // Policy values
    public const int PolicyDisabledValue = 1;
    public const int PolicyEnabledValue = 0;

    // Service start types (from registry)
    public const int ServiceStartBoot = 0;
    public const int ServiceStartSystem = 1;
    public const int ServiceStartAuto = 2;
    public const int ServiceStartManual = 3;
    public const int ServiceStartDisabled = 4;

    // UI / confirmation strings
    public const string NuclearPhrase = "YES I UNDERSTAND THE RISKS";

    // Operation descriptions
    public const string Level1Description = "LEVEL 1 — NUCLEAR PURGE (Hard Exorcism): Disable PCA + Clear Layers + Aggressive Policies + Backup (sysmain.sdb rename removed)";
    public const string Level2Description = "LEVEL 2 — Safe Recommended Purge: Disables PCA + Tasks + Clears Registry Layers + extra policies";
    public const string Level3Description = "LEVEL 3 — Restore Defaults: Re-enable PCA service, tasks, and remove policies";
}
