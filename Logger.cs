using System;
using System.IO;
using System.Windows.Forms;

namespace PurgeAppCompat;

/// <summary>
/// Centralized, thread-safe logger for both UI (RichTextBox) and persistent file logging.
/// </summary>
public sealed class Logger
{
    private readonly string _logPath;
    private RichTextBox? _uiLogBox;
    private readonly object _lock = new();

    public string LogPath => _logPath;

    public Logger(string logPath)
    {
        _logPath = logPath ?? throw new ArgumentNullException(nameof(logPath));
        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
    }

    /// <summary>
    /// Attach (or re-attach) the UI RichTextBox used for real-time logging. Safe to call from UI thread.
    /// </summary>
    public void AttachUi(RichTextBox richTextBox)
    {
        _uiLogBox = richTextBox;
    }

    /// <summary>
    /// Write a message to both file (if possible) and UI (thread-safe).
    /// </summary>
    public void Log(string message)
    {
        string timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";

        // File logging (best effort, never throw)
        try
        {
            lock (_lock)
            {
                File.AppendAllText(_logPath, timestamped + Environment.NewLine);
            }
        }
        catch
        {
            // Swallow - logging must never break operations
        }

        // UI logging (marshal to UI thread if needed)
        var box = _uiLogBox;
        if (box != null)
        {
            if (box.InvokeRequired)
            {
                box.BeginInvoke(new Action<string>(AppendToUi), timestamped);
            }
            else
            {
                AppendToUi(timestamped);
            }
        }
    }

    private void AppendToUi(string line)
    {
        var box = _uiLogBox;
        if (box == null || box.IsDisposed) return;

        box.AppendText(line + "\r\n");
        box.SelectionStart = box.Text.Length;
        box.ScrollToCaret();
    }

    /// <summary>
    /// Logs a section separator with optional title.
    /// </summary>
    public void LogSection(string title)
    {
        Log(new string('=', 60));
        Log(title);
        Log(new string('=', 60));
    }

    /// <summary>
    /// Writes initial session header with environment info.
    /// </summary>
    public void LogSessionHeader()
    {
        LogSection("PURGEAPPCOMPAT SESSION START");
        try
        {
            Log($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Log($"Machine: {Environment.MachineName}");
            Log($"User: {Environment.UserName}");
            Log($"OS: {Environment.OSVersion.VersionString} (64-bit: {Environment.Is64BitOperatingSystem})");
            Log($".NET Runtime: {Environment.Version}");
            Log($"Working Directory: {Environment.CurrentDirectory}");
            Log($"Log File: {_logPath}");
            Log($"CLR Bitness: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}");
        }
        catch (Exception ex)
        {
            Log($"Header info partial: {ex.Message}");
        }
        Log(string.Empty);
    }

    /// <summary>
    /// Logs an error with context.
    /// </summary>
    public void LogError(string context, Exception ex)
    {
        Log($"ERROR [{context}]: {ex.Message}");
        if (ex.InnerException != null)
            Log($"  Inner: {ex.InnerException.Message}");
#if DEBUG
        Log($"  Stack: {ex.StackTrace}");
#endif
    }

    public void LogWarning(string message)
    {
        Log($"WARNING: {message}");
    }

    public void LogSuccess(string message)
    {
        Log($"SUCCESS: {message}");
    }
}
