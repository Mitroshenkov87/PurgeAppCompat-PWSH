using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using Microsoft.Win32;

namespace PurgeAppCompat;

/// <summary>
/// Encapsulates all system status detection logic for AppCompat / PCA features.
/// Designed to be resilient: individual checks are isolated so one failure does not break the entire status report.
/// </summary>
public sealed class StatusChecker
{
    private readonly Logger _logger;

    public StatusChecker(Logger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Represents a point-in-time snapshot of relevant system state.
    /// </summary>
    public sealed record StatusSnapshot(
        string PcaServiceText,
        string GpoPolicyText,
        string TasksText,
        string LayersText,
        string OverallText,
        bool SignificantPurgeDetected,
        int DisabledTaskCount,
        int RegistryLayerCount
    );

    public StatusSnapshot Refresh()
    {
        _logger.Log("Refreshing current system status...");

        string pcaText = GetPcaServiceStatus();
        string gpoText = GetGpoPolicyStatus();
        (string tasksText, int disabledCount) = GetTasksStatus();
        (string layersText, int layerCount) = GetLayersStatus();

        bool significant = disabledCount > 0 || layerCount == 0 || IsPolicyAggressivelySet();

        string overall = significant
            ? "Overall State: Significant purge detected (some legacy support already removed or restricted)"
            : "Overall State: Most legacy compatibility features still active";

        _logger.Log($"Status summary: PCA={pcaText.Split(':').LastOrDefault()?.Trim()}, Tasks disabled={disabledCount}/4, Layers={layerCount}");

        return new StatusSnapshot(
            pcaText,
            gpoText,
            tasksText,
            layersText,
            overall,
            significant,
            disabledCount,
            layerCount
        );
    }

    private string GetPcaServiceStatus()
    {
        try
        {
            var svc = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == AppCompatConstants.PcaSvcName);
            if (svc == null)
                return "PCA Service (PcaSvc): Not found on this system";

            string state = svc.Status == ServiceControllerStatus.Running ? "Running" : svc.Status.ToString();
            string startType = GetServiceStartType(AppCompatConstants.PcaSvcName);
            return $"PCA Service (PcaSvc): {state} | Startup: {startType}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"PCA service query failed: {ex.Message}");
            return "PCA Service (PcaSvc): Query failed (see log)";
        }
    }

    private string GetServiceStartType(string serviceName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            if (key == null) return "Unknown (key missing)";

            int start = Convert.ToInt32(key.GetValue("Start", AppCompatConstants.ServiceStartAuto));
            return start switch
            {
                AppCompatConstants.ServiceStartBoot => "Boot",
                AppCompatConstants.ServiceStartSystem => "System",
                AppCompatConstants.ServiceStartAuto => "Automatic",
                AppCompatConstants.ServiceStartManual => "Manual",
                AppCompatConstants.ServiceStartDisabled => "Disabled",
                _ => $"Unknown ({start})"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Start type query for {serviceName} failed: {ex.Message}");
            return "Unknown";
        }
    }

    private string GetGpoPolicyStatus()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(AppCompatConstants.AppCompatPolicyKey);
            if (key == null)
                return "DisablePCA Policy (GPO): Not applied (key absent)";

            int disablePca = Convert.ToInt32(key.GetValue("DisablePCA", 0));
            bool gpoDisabled = disablePca == AppCompatConstants.PolicyDisabledValue;

            // Also surface other useful policies if present
            int ait = Convert.ToInt32(key.GetValue("AITEnable", 1));
            int inv = Convert.ToInt32(key.GetValue("DisableInventory", 0));

            string extra = string.Empty;
            if (ait == 0) extra += " AITEnable=0";
            if (inv == 1) extra += " DisableInventory=1";

            return gpoDisabled
                ? $"DisablePCA Policy (GPO): Applied (Disabled){extra}"
                : $"DisablePCA Policy (GPO): Not applied{extra}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"GPO policy query failed: {ex.Message}");
            return "DisablePCA Policy (GPO): Query error (see log)";
        }
    }

    private bool IsPolicyAggressivelySet()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(AppCompatConstants.AppCompatPolicyKey);
            if (key == null) return false;

            return Convert.ToInt32(key.GetValue("DisablePCA", 0)) == 1 ||
                   Convert.ToInt32(key.GetValue("AITEnable", 1)) == 0 ||
                   Convert.ToInt32(key.GetValue("DisableInventory", 0)) == 1;
        }
        catch { return false; }
    }

    private (string text, int disabledCount) GetTasksStatus()
    {
        int disabled = 0;
        int found = 0;

        foreach (var task in AppCompatConstants.AppExperienceTasks)
        {
            try
            {
                // Use CSV format for more reliable parsing than LIST
                var psi = new ProcessStartInfo("schtasks.exe",
                    $"/Query /TN \"{task}\" /FO CSV /NH")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                using var p = Process.Start(psi);
                if (p == null) continue;

                string output = p.StandardOutput.ReadToEnd();
                string err = p.StandardError.ReadToEnd();
                p.WaitForExit(8000);

                found++;
                // CSV line typically: "TaskName","Next Run Time","Status","Logon Mode",...
                // Status column is the 3rd quoted field in many localizations; we look for Disabled anywhere in output for robustness.
                if (output.Contains("\"Disabled\"", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("Disabled", StringComparison.OrdinalIgnoreCase))
                {
                    disabled++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Task query failed for {task}: {ex.Message}");
            }
        }

        string text = found > 0
            ? $"Application Experience Tasks: {disabled}/{AppCompatConstants.AppExperienceTasks.Length} disabled"
            : "Application Experience Tasks: Unable to query (schtasks)";

        return (text, disabled);
    }

    private (string text, int count) GetLayersStatus()
    {
        int count = 0;
        try
        {
            using var hklm = Registry.LocalMachine.OpenSubKey(AppCompatConstants.LayersHklmKey);
            if (hklm != null) count += hklm.ValueCount;

            using var hkcu = Registry.CurrentUser.OpenSubKey(AppCompatConstants.LayersHkcuKey);
            if (hkcu != null) count += hkcu.ValueCount;

            return ($"Compatibility Layers in Registry: {count} entries found (HKLM + HKCU)", count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Layers count query failed: {ex.Message}");
            return ("Compatibility Layers in Registry: Query failed (see log)", 0);
        }
    }

    /// <summary>
    /// Quick helper used by UI for overall color decisions etc.
    /// </summary>
    public static bool IsPurgedLikeState(StatusSnapshot snap) => snap.SignificantPurgeDetected;
}
