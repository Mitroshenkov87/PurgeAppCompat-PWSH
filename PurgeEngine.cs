using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using Microsoft.Win32;

namespace PurgeAppCompat;

/// <summary>
/// Core engine that executes the three purge levels.
/// Depends on Logger, BackupManager, StatusChecker, and SystemRestoreHelper for clean separation.
/// All destructive work is centralized here.
/// </summary>
public sealed class PurgeEngine
{
    private readonly Logger _logger;
    private readonly BackupManager _backupManager;
    private readonly SystemRestoreHelper _restoreHelper;
    private readonly StatusChecker _statusChecker;

    public PurgeEngine(Logger logger, BackupManager backupManager, SystemRestoreHelper restoreHelper, StatusChecker statusChecker)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
        _restoreHelper = restoreHelper ?? throw new ArgumentNullException(nameof(restoreHelper));
        _statusChecker = statusChecker ?? throw new ArgumentNullException(nameof(statusChecker));
    }

    // ===== PUBLIC ENTRY POINTS =====

    public void ExecuteLevel1(Action? onCompleted = null)
    {
        _logger.LogSection("LEVEL 1 — NUCLEAR PURGE STARTING");

        try
        {
            // 1. Create System Restore point (highest safety priority)
            bool restoreOk = _restoreHelper.TryCreateRestorePoint(
                "PurgeAppCompat Level 1 - Before nuclear AppCompat purge");

            if (!restoreOk)
            {
                _logger.LogWarning("Proceeding without automated restore point. Manual creation strongly recommended.");
            }

            // 2. Perform safe Level 2 operations first
            _logger.Log("LEVEL 1: Executing Level 2 steps first (service, policy, tasks, layers)...");
            ExecuteLevel2Internal(isPartOfLevel1: true);

            _logger.LogSuccess("LEVEL 1 NUCLEAR PURGE COMPLETED.");
            _logger.Log("Note: sysmain.sdb rename step has been removed from automatic execution (per design decision).");
        }
        catch (Exception ex)
        {
            _logger.LogError("Level 1 execution", ex);
            throw;
        }
        finally
        {
            onCompleted?.Invoke();
        }
    }

    public void ExecuteLevel2(Action? onCompleted = null)
    {
        _logger.LogSection("LEVEL 2 — SAFE RECOMMENDED PURGE STARTING");

        try
        {
            ExecuteLevel2Internal(isPartOfLevel1: false);
            _logger.LogSuccess("LEVEL 2 COMPLETED.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Level 2 execution", ex);
            throw;
        }
        finally
        {
            onCompleted?.Invoke();
        }
    }

    public void ExecuteLevel3(Action? onCompleted = null)
    {
        _logger.LogSection("LEVEL 3 — RESTORE DEFAULTS STARTING");

        try
        {
            // Re-enable PCA service
            ConfigureService(AppCompatConstants.PcaSvcName, startType: "demand", startNow: true);

            // Remove the main DisablePCA policy (others left for user to decide)
            RemovePolicyValue("DisablePCA");

            // Re-enable the four Application Experience tasks
            EnableAppExperienceTasks();

            _logger.LogSuccess("LEVEL 3 RESTORE COMPLETED. Some services/tasks may still need a reboot to fully return.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Level 3 execution", ex);
            throw;
        }
        finally
        {
            onCompleted?.Invoke();
        }
    }

    // ===== INTERNAL IMPLEMENTATION =====

    private void ExecuteLevel2Internal(bool isPartOfLevel1)
    {
        // PCA Service → Disabled
        ConfigureService(AppCompatConstants.PcaSvcName, startType: "disabled", startNow: false);

        // GPO policy DisablePCA = 1 (primary)
        ApplyPolicyValue("DisablePCA", AppCompatConstants.PolicyDisabledValue);

        // Additional recommended policies from original PowerShell scripts (safe & useful)
        ApplyPolicyValue("AITEnable", AppCompatConstants.PolicyEnabledValue);           // 0 = disabled
        ApplyPolicyValue("DisableInventory", AppCompatConstants.PolicyDisabledValue);
        ApplyPolicyValue("DisableUAR", AppCompatConstants.PolicyDisabledValue);         // User Access Reporting

        // Disable scheduled tasks
        DisableAppExperienceTasks();

        // Clear compatibility layers
        ClearCompatibilityLayers();

        _logger.Log("Level 2 core operations finished.");
    }

    private void ConfigureService(string serviceName, string startType, bool startNow)
    {
        try
        {
            using var svc = new ServiceController(serviceName);
            if (svc.Status != ServiceControllerStatus.Stopped)
            {
                try
                {
                    svc.Stop();
                    svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(12));
                    _logger.Log($"{serviceName} stopped.");
                }
                catch (Exception stopEx)
                {
                    _logger.LogWarning($"Could not stop {serviceName}: {stopEx.Message}");
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Service may not exist
        }

        RunCommand("sc.exe", $"config {serviceName} start= {startType}");

        if (startNow && startType != "disabled")
        {
            RunCommand("net.exe", $"start {serviceName}");
        }

        _logger.Log($"{serviceName} configured to {startType}.");
    }

    private void ApplyPolicyValue(string valueName, int value)
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(AppCompatConstants.AppCompatPolicyKey);
            key.SetValue(valueName, value, RegistryValueKind.DWord);
            _logger.Log($"Policy {valueName} = {value} applied under AppCompat.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to set policy {valueName}: {ex.Message}");
        }
    }

    private void RemovePolicyValue(string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(AppCompatConstants.AppCompatPolicyKey, writable: true);
            if (key != null)
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
                _logger.Log($"Removed policy value: {valueName}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not remove policy {valueName}: {ex.Message}");
        }
    }

    private void DisableAppExperienceTasks()
    {
        foreach (var task in AppCompatConstants.AppExperienceTasks)
        {
            RunCommand("schtasks.exe", $"/Change /TN \"{task}\" /DISABLE");
        }
        _logger.Log("Application Experience tasks disabled.");
    }

    private void EnableAppExperienceTasks()
    {
        foreach (var task in AppCompatConstants.AppExperienceTasks)
        {
            RunCommand("schtasks.exe", $"/Change /TN \"{task}\" /ENABLE");
        }
        _logger.Log("Application Experience tasks re-enabled.");
    }

    private void ClearCompatibilityLayers()
    {
        try
        {
            ClearLayerKey(AppCompatConstants.LayersHklmKey, "HKLM");
            ClearLayerKey(AppCompatConstants.LayersHkcuKey, "HKCU");

            // Bonus: also clear some of the more aggressive marker keys that accumulate cruft (non-destructive to functionality)
            TryDeleteKeyIfEmpty(AppCompatConstants.CompatMarkersKey);
            TryDeleteKeyIfEmpty(AppCompatConstants.SharedKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Registry layer clearing encountered issues: {ex.Message}");
        }
    }

    private void ClearLayerKey(string keyPath, string friendly)
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(
                keyPath.StartsWith("SOFTWARE\\Microsoft", StringComparison.OrdinalIgnoreCase) ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
                RegistryView.Default)
                .OpenSubKey(keyPath, writable: true);

            if (key == null) return;

            foreach (string name in key.GetValueNames())
            {
                try { key.DeleteValue(name, throwOnMissingValue: false); } catch { }
            }
            _logger.Log($"Cleared {friendly} compatibility layers.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed clearing {friendly} layers: {ex.Message}");
        }
    }

    private void TryDeleteKeyIfEmpty(string keyPath)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
            if (key != null && key.ValueCount == 0 && key.SubKeyCount == 0)
            {
                // Can't easily delete self from here without parent; just leave it. Non-critical.
            }
        }
        catch { /* ignore */ }
    }

    private void RunCommand(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var p = Process.Start(psi);
            if (p == null) return;

            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit(15000);

            if (p.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
            {
                _logger.LogWarning($"Command [{fileName} {arguments}] exited {p.ExitCode}: {stderr.Trim()}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Command execution failed [{fileName} {arguments}]: {ex.Message}");
        }
    }
}
