using System;
using System.Diagnostics;
using System.Globalization;

namespace PurgeAppCompat;

/// <summary>
/// Creates System Restore points before risky operations (primarily Level 1).
/// Uses multiple strategies for maximum compatibility:
///   1. WMIC built-in command (most reliable, no extra assemblies)
///   2. PowerShell Checkpoint-Computer as fallback
/// Logs clearly on success or failure.
/// </summary>
public sealed class SystemRestoreHelper
{
    private readonly Logger _logger;

    public SystemRestoreHelper(Logger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Attempts to create a System Restore point. Returns true if any method succeeded.
    /// </summary>
    /// <param name="description">User-visible description of the restore point.</param>
    /// <returns>True if a restore point was successfully created (or we think it was).</returns>
    public bool TryCreateRestorePoint(string description)
    {
        _logger.LogSection("SYSTEM RESTORE POINT");

        if (string.IsNullOrWhiteSpace(description))
            description = "PurgeAppCompat - Pre-purge safety point";

        // Strategy 1: WMIC (works on most Windows 10/11, even without PowerShell remoting quirks)
        if (TryCreateViaWmic(description))
            return true;

        _logger.LogWarning("Primary WMIC restore point method failed. Trying PowerShell fallback...");

        // Strategy 2: PowerShell Checkpoint-Computer (good on modern Windows)
        if (TryCreateViaPowerShell(description))
            return true;

        _logger.LogWarning("Both automated restore point methods failed or are unavailable.");
        _logger.Log(">>> ACTION REQUIRED: Please manually create a System Restore point via System Properties before proceeding with Level 1.");
        _logger.Log("    (Search for 'Create a restore point' in Start menu).");

        return false;
    }

    private bool TryCreateViaWmic(string description)
    {
        try
        {
            // WMIC syntax for creating restore point:
            // wmic.exe /Namespace:\\root\default Path SystemRestore Call CreateRestorePoint "description", 100, 7
            // 100 = Application install, 7 = Beginning of operation (modify settings)
            string args = $@"/Namespace:\\root\default Path SystemRestore Call CreateRestorePoint ""{description}"", 100, 7";

            var psi = new ProcessStartInfo("wmic.exe", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                _logger.LogWarning("Failed to start wmic.exe");
                return false;
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            bool exited = process.WaitForExit(45000); // 45 seconds generous timeout

            if (!exited)
            {
                _logger.LogWarning("WMIC restore point creation timed out.");
                return false;
            }

            // WMIC returns something like: "Creating instance of 'SystemRestore' ... Method execution successful. Out Parameters: instance of __PARAMETERS { ReturnValue = 0; };"
            bool success = stdout.Contains("ReturnValue = 0") || stdout.Contains("successful", StringComparison.OrdinalIgnoreCase);

            if (success)
            {
                _logger.LogSuccess("System Restore point created successfully via WMIC.");
                _logger.Log($"   Description: {description}");
                return true;
            }

            _logger.LogWarning($"WMIC restore point returned non-zero or unexpected output. StdOut: {stdout.Trim()} | StdErr: {stderr.Trim()}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("WMIC restore point creation", ex);
            return false;
        }
    }

    private bool TryCreateViaPowerShell(string description)
    {
        try
        {
            // Use -NoProfile for speed and predictability. Requires PowerShell available (default on Win11).
            string psCommand = $"-NoProfile -Command \"Checkpoint-Computer -Description '{description.Replace("'", "''")}' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop; Write-Output 'PS_RESTORE_SUCCESS'\"";

            var psi = new ProcessStartInfo("powershell.exe", psCommand)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(60000);

            if (stdout.Contains("PS_RESTORE_SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogSuccess("System Restore point created via PowerShell Checkpoint-Computer.");
                return true;
            }

            if (!string.IsNullOrWhiteSpace(stderr))
                _logger.LogWarning($"PowerShell restore stderr: {stderr.Trim()}");

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("PowerShell restore point fallback", ex);
            return false;
        }
    }
}
