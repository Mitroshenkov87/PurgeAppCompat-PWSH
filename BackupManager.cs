using System;
using System.IO;

namespace PurgeAppCompat;

/// <summary>
/// Handles creation of timestamped, robust backups before destructive operations.
/// Creates a manifest file and supports backing up AmCache.hve.
/// </summary>
public sealed class BackupManager
{
    private readonly Logger _logger;
    private readonly string _backupRoot;

    public string BackupRoot => _backupRoot;

    public BackupManager(Logger logger, string? preferredRoot = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _backupRoot = !string.IsNullOrWhiteSpace(preferredRoot)
            ? preferredRoot
            : AppCompatConstants.PreferredBackupRoot;

        // Fall back to the classic location if the preferred one cannot be created
        try
        {
            Directory.CreateDirectory(_backupRoot);
        }
        catch
        {
            _logger.LogWarning($"Could not create preferred backup root {_backupRoot}. Falling back to {AppCompatConstants.DefaultBackupRoot}");
            _backupRoot = AppCompatConstants.DefaultBackupRoot;
            Directory.CreateDirectory(_backupRoot);
        }
    }

    /// <summary>
    /// Creates a full backup session folder for Level 1 (and optionally others).
    /// Returns the backup directory path on success, or null if critical backup failed.
    /// </summary>
    public string? CreateLevel1BackupSession()
    {
        string sessionDir = Path.Combine(_backupRoot, $"Level1_{DateTime.Now:yyyyMMdd_HHmmss}");
        try
        {
            Directory.CreateDirectory(sessionDir);

            _logger.Log($"Backup session folder: {sessionDir}");

            bool amcacheBackedUp = BackupAmCacheIfPresent(sessionDir);

            // Write a simple manifest
            string manifest = Path.Combine(sessionDir, "backup_manifest.txt");
            File.WriteAllText(manifest,
                $"PurgeAppCompat Level 1 Backup Manifest\r\n" +
                $"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n" +
                $"Machine: {Environment.MachineName}\r\n" +
                $"AmCache.hve backed up: {amcacheBackedUp}\r\n" +
                $"Backup location: {sessionDir}\r\n");

            _logger.LogSuccess($"Backup manifest written to {manifest}");

            return sessionDir;
        }
        catch (Exception ex)
        {
            _logger.LogError("Backup session creation", ex);
            return null;
        }
    }

    private bool BackupFile(string sourcePath, string destDir, string destFileName)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                _logger.Log($"Backup source not present (skipping): {sourcePath}");
                return false;
            }

            string dest = Path.Combine(destDir, destFileName);
            File.Copy(sourcePath, dest, overwrite: true);

            // Also copy any obvious sidecar files if they exist (e.g. .bak from previous)
            _logger.LogSuccess($"Backed up: {sourcePath} → {dest} ({new FileInfo(dest).Length} bytes)");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to backup {sourcePath}: {ex.Message}");
            return false;
        }
    }

    private bool BackupAmCacheIfPresent(string sessionDir)
    {
        // Common AmCache locations across Windows versions
        string[] candidates =
        {
            Path.Combine(AppCompatConstants.WindowsDir, @"AppCompat\Programs\AmCache.hve"),
            Path.Combine(AppCompatConstants.WindowsDir, @"AppCompat\Programs\AmCache.hve.bak"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\config\AmCache.hve")
        };

        bool any = false;
        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                string name = Path.GetFileName(path);
                if (BackupFile(path, sessionDir, name))
                    any = true;
            }
        }
        if (!any)
            _logger.Log("No AmCache.hve found to back up (this is normal on many clean systems).");

        return any;
    }

    /// <summary>
    /// Simple single-file backup (used by older paths or Level 2 if needed).
    /// </summary>
    public bool TryBackupSingleFile(string sourcePath, string description)
    {
        try
        {
            string dir = Path.Combine(_backupRoot, $"Misc_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(dir);
            return BackupFile(sourcePath, dir, Path.GetFileName(sourcePath) + ".bak");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Single file backup failed for {description}: {ex.Message}");
            return false;
        }
    }
}
