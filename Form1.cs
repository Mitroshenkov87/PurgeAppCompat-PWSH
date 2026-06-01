using System;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PurgeAppCompat;

/// <summary>
/// Main WinForms UI. Deliberately kept thin — all heavy logic lives in dedicated classes
/// (Logger, StatusChecker, BackupManager, SystemRestoreHelper, PurgeEngine).
/// </summary>
public partial class Form1 : Form
{
    // === Core Services (injected / created here) ===
    private Logger _logger = null!;
    private StatusChecker _statusChecker = null!;
    private BackupManager _backupManager = null!;
    private SystemRestoreHelper _restoreHelper = null!;
    private PurgeEngine _purgeEngine = null!;

    // === UI Controls we need references to after BuildInterface ===
    private RichTextBox logBox = null!;
    private Label statusPca = null!;
    private Label statusGpo = null!;
    private Label statusTasks = null!;
    private Label statusLayers = null!;
    private Label overallStatus = null!;
    private Label operationStatusLabel = null!;

    private Button btnLevel1 = null!;
    private Button btnLevel2 = null!;
    private Button btnLevel3 = null!;
    private Button btnRefresh = null!;
    private Button btnOpenLog = null!;

    private readonly string _logPath;

    public Form1()
    {
        _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"PurgeAppCompat_{DateTime.Now:yyyyMMdd_HHmmss}.log");

        InitializeComponent();
        BuildInterface();

        if (!IsAdministrator())
        {
            MessageBox.Show("This program requires Administrator privileges.\nPlease restart it as Administrator.",
                "Administrator Required", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }

        InitializeServices();
        _logger.LogSessionHeader();

        // Load application icon (prefer .ico for best results)
        try
        {
            string icoPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "PurgeAppCompat.ico");
            string pngPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "PurgeAppCompat.png");

            if (File.Exists(icoPath))
            {
                this.Icon = new Icon(icoPath);
            }
            else if (File.Exists(pngPath))
            {
                using var stream = File.OpenRead(pngPath);
                this.Icon = new Icon(stream);
            }
        }
        catch { /* Non-critical */ }

        RefreshCurrentStatus();
    }

    private bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private void InitializeServices()
    {
        _logger = new Logger(_logPath);
        _logger.AttachUi(logBox);

        _statusChecker = new StatusChecker(_logger);
        _backupManager = new BackupManager(_logger);
        _restoreHelper = new SystemRestoreHelper(_logger);
        _purgeEngine = new PurgeEngine(_logger, _backupManager, _restoreHelper, _statusChecker);

        _logger.Log("All core services initialized.");
        _logger.Log($"Backup root in use: {_backupManager.BackupRoot}");
    }

    private void BuildInterface()
    {
        // === Layout constants for consistent visual spacing ===
        const int LeftMargin = 20;
        const int RightMargin = 20;

        this.Text = "PurgeAppCompat v3.0 — Hard Windows 11 Compatibility Purge (Refactored)";
        this.Size = new Size(960, 850);
        this.MinimumSize = new Size(820, 650);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Font = new Font("Segoe UI", 10);
        this.BackColor = Color.FromArgb(245, 245, 247);
        this.FormBorderStyle = FormBorderStyle.Sizable;  // Enable resizing
        this.AutoScaleMode = AutoScaleMode.Dpi;          // HiDPI support (also set in designer)
        this.Padding = new Padding(0);                   // Root table provides uniform inner margins

        // === ROOT TABLELAYOUTPANEL: the foundation for resizable, high-DPI friendly layout ===
        // Single column, mixed row types: AutoSize for content-driven sections, Absolute for
        // the visually dominant Level 1 button + its warning, Percent for the log (grows on resize).
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 10,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(LeftMargin, 8, RightMargin, 4) // breathing room on all sides
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 0: header (title + subtitle)
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 1: status group
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 2: operation status (transient)
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));  // 3: nuclear warning (full-width red bar)
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));  // 4: LEVEL 1 dominant nuclear button
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));  // 5: level 2 + 3 side-by-side
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 6: utility buttons
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 7: log label
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // 8: log box grows to fill remaining space
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 9: footer

        // === HEADER (sub-table for clean vertical stacking, no magic numbers) ===
        var header = new Label
        {
            Text = "PURGE APPCOMPAT — NUCLEAR EDITION",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(139, 0, 0),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };

        var subtitle = new Label
        {
            Text = "Level 1 is the full hard purge. Creates System Restore point + robust backup, disables service/policies/tasks/layers. sysmain.sdb step removed (SFC often restores it anyway).",
            Font = new Font("Segoe UI", 10, FontStyle.Italic),
            ForeColor = Color.FromArgb(80, 80, 80),
            AutoSize = true,
            Margin = new Padding(0)
        };

        var headerHost = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 6),
            BackColor = Color.Transparent
        };
        headerHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        headerHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerHost.Controls.Add(header, 0, 0);
        headerHost.Controls.Add(subtitle, 0, 1);
        // Dock omitted on purpose for AutoSize row — table measures preferred size

        // === CURRENT STATUS GROUP (FlowLayout inside for labels + AutoSize GroupBox) ===
        var statusGroup = new GroupBox
        {
            Text = "CURRENT SYSTEM STATUS  (refreshes on start and after operations)",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            BackColor = Color.White,
            Padding = new Padding(10, 8, 10, 8),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
            // Note: no Dock here — AutoSize row in parent TableLayout measures preferred height from this GroupBox + its inner FlowLayout.
            // Width comes from the table column (percent of available form width minus root padding).
        };

        var statusItemsHost = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.White
        };

        statusPca = CreateStatusLabel();
        statusGpo = CreateStatusLabel();
        statusTasks = CreateStatusLabel();
        statusLayers = CreateStatusLabel();

        overallStatus = new Label
        {
            Text = "Overall State: Checking...",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0)
        };

        statusItemsHost.Controls.Add(statusPca);
        statusItemsHost.Controls.Add(statusGpo);
        statusItemsHost.Controls.Add(statusTasks);
        statusItemsHost.Controls.Add(statusLayers);
        statusItemsHost.Controls.Add(overallStatus);

        statusGroup.Controls.Add(statusItemsHost);

        // === OPERATION STATUS (transient, shown during long ops) ===
        operationStatusLabel = new Label
        {
            Text = string.Empty,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.DarkBlue,
            AutoSize = true,
            Visible = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 0, 2)
        };

        // === LEVEL 1 — VISUALLY DOMINANT (Nuclear) ===
        // Dedicated tall row + full-width Dock + strong styling keeps it the star of the UI.
        var level1Warning = new Label
        {
            Text = "⚠ DESTRUCTIVE NUCLEAR OPTION — Disables legacy AppCompat (service + policies + tasks + layers). Creates Restore Point + Backup. REBOOT STRONGLY RECOMMENDED.",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(139, 0, 0),
            TextAlign = ContentAlignment.MiddleCenter,
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };

        btnLevel1 = new Button
        {
            Text = "LEVEL 1 — NUCLEAR PURGE (Hard Exorcism)\nDisable PCA + Tasks + Clear Layers + Aggressive Policies",
            BackColor = Color.FromArgb(178, 0, 0),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 0, 4)
        };
        btnLevel1.Click += (s, e) => RunLevel(1);
        var tt1 = new ToolTip { AutoPopDelay = 12000 };
        tt1.SetToolTip(btnLevel1, "MOST AGGRESSIVE OPTION.\nCreates System Restore point + full backup first.\nThen disables PCA service + aggressive policies, tasks, clears layers.\nNote: sysmain.sdb rename step removed (often ineffective + SFC can restore it).\nReboot strongly recommended.");

        // Level 2 (green, safe) + Level 3 in 50/50 resizable container
        btnLevel2 = new Button
        {
            Text = "LEVEL 2 — Safe Recommended Purge\n(Disables PCA + Tasks + Clears Layers + Recommended Policies)",
            BackColor = Color.FromArgb(0, 128, 0),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 3, 0)
        };
        btnLevel2.Click += (s, e) => RunLevel(2);
        var tt2 = new ToolTip { AutoPopDelay = 9000 };
        tt2.SetToolTip(btnLevel2, "SAFE RECOMMENDED for most users.\nDisables PCA, applies helpful policies, disables telemetry tasks,\nand clears accumulated compatibility layers.\nNo file deletion. Reboot recommended.");

        btnLevel3 = new Button
        {
            Text = "LEVEL 3 — Restore Defaults\n(Re-enable PCA service, tasks, and remove policies)",
            BackColor = Color.FromArgb(180, 120, 0),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Fill,
            Margin = new Padding(3, 0, 0, 0)
        };
        btnLevel3.Click += (s, e) => RunLevel(3);
        var tt3 = new ToolTip { AutoPopDelay = 7000 };
        tt3.SetToolTip(btnLevel3, "UNDOS most changes made by Levels 1 & 2.\nRe-enables the PCA service and the scheduled tasks.");

        var level23Panel = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 2, 0, 2)
        };
        level23Panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        level23Panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        level23Panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        level23Panel.Controls.Add(btnLevel2, 0, 0);
        level23Panel.Controls.Add(btnLevel3, 1, 0);
        level23Panel.Dock = DockStyle.Fill;

        // === UTILITY BUTTONS (FlowLayout keeps them naturally left-aligned, no x/y math) ===
        btnRefresh = new Button
        {
            Text = "Refresh Status",
            Size = new Size(150, 28),
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 8, 0)
        };
        btnRefresh.Click += (s, e) => RefreshCurrentStatus();
        var ttRefresh = new ToolTip();
        ttRefresh.SetToolTip(btnRefresh, "Re-query services, policies, tasks, and registry layers status.");

        btnOpenLog = new Button
        {
            Text = "Open Log File",
            Size = new Size(140, 28),
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0)
        };
        btnOpenLog.Click += (s, e) => OpenLogFile();
        var ttLog = new ToolTip();
        ttLog.SetToolTip(btnOpenLog, "Open the detailed session log (written to Desktop) in Notepad for full history and diagnostics.");

        var utilityHost = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 2, 0, 6),
            BackColor = Color.Transparent
        };
        utilityHost.Controls.Add(btnRefresh);
        utilityHost.Controls.Add(btnOpenLog);
        utilityHost.Dock = DockStyle.Fill;

        // === LOG (label auto, box grows via percent row) ===
        var logLabel = new Label
        {
            Text = "OPERATION LOG (also written to Desktop)",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 2)
        };

        logBox = new RichTextBox
        {
            ReadOnly = true,
            BackColor = Color.FromArgb(18, 18, 18),
            ForeColor = Color.FromArgb(200, 200, 200),
            Font = new Font("Consolas", 9.2f),
            DetectUrls = false,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill
        };

        var footer = new Label
        {
            Text = "Reboot recommended after any purge level. Level 1 changes are destructive — always review the log. Created with improved architecture for reliability & safety.",
            ForeColor = Color.FromArgb(100, 100, 100),
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0)
        };

        // === ASSEMBLE INTO ROOT TABLE (controls are now laid out by rows, no absolute positions) ===
        root.Controls.Add(headerHost, 0, 0);
        root.Controls.Add(statusGroup, 0, 1);
        root.Controls.Add(operationStatusLabel, 0, 2);
        root.Controls.Add(level1Warning, 0, 3);
        root.Controls.Add(btnLevel1, 0, 4);
        root.Controls.Add(level23Panel, 0, 5);
        root.Controls.Add(utilityHost, 0, 6);
        root.Controls.Add(logLabel, 0, 7);
        root.Controls.Add(logBox, 0, 8);
        root.Controls.Add(footer, 0, 9);

        this.Controls.Add(root);
    }

    private Label CreateStatusLabel()
    {
        return new Label
        {
            Text = "Loading...",
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f),
            Margin = new Padding(0, 0, 0, 3)  // vertical breathing room in FlowLayout host
        };
    }

    private void Log(string message) => _logger?.Log(message);  // Delegate to the real Logger

    private void OpenLogFile()
    {
        try
        {
            if (_logger != null && File.Exists(_logger.LogPath))
                System.Diagnostics.Process.Start("notepad.exe", _logger.LogPath);
            else
                MessageBox.Show("Log file not yet available.", "PurgeAppCompat", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open log: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ===== NEW SLIM STATUS + ENGINE INTEGRATION =====

    private void RefreshCurrentStatus()
    {
        try
        {
            var snap = _statusChecker.Refresh();

            statusPca.Text = snap.PcaServiceText;
            statusGpo.Text = snap.GpoPolicyText;
            statusTasks.Text = snap.TasksText;
            statusLayers.Text = snap.LayersText;
            overallStatus.Text = snap.OverallText;

            // Visual cue on overall status
            overallStatus.ForeColor = snap.SignificantPurgeDetected ? Color.DarkGreen : Color.DarkSlateBlue;
        }
        catch (Exception ex)
        {
            Log($"Status refresh error: {ex.Message}");
        }
    }

    private void RunLevel(int level)
    {
        // Pre-flight confirmations
        if (level == 1)
        {
            if (!ConfirmNuclearPurge()) return;
        }
        else if (level == 3)
        {
            if (MessageBox.Show("Restore all defaults? This will re-enable PCA service and scheduled tasks.",
                    "Confirm Restore Defaults", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
        }

        // Disable UI during long operation
        SetUiEnabledDuringOperation(false);
        SetOperationStatus($"Running Level {level} — please wait... (see log for progress)");

        Log($"=== STARTING LEVEL {level} ===");

        // Run the actual work off the UI thread so the form stays responsive
        Task.Run(() =>
        {
            try
            {
                switch (level)
                {
                    case 1:
                        _purgeEngine.ExecuteLevel1();
                        break;
                    case 2:
                        _purgeEngine.ExecuteLevel2();
                        break;
                    case 3:
                        _purgeEngine.ExecuteLevel3();
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"FATAL ERROR during Level {level}: {ex.Message}");
            }
            finally
            {
                // Always return to UI thread for final updates
                this.BeginInvoke(new Action(() =>
                {
                    Log("=== OPERATION COMPLETED ===");
                    Log("A reboot is strongly recommended after any of these changes.");
                    SetOperationStatus(string.Empty);
                    SetUiEnabledDuringOperation(true);
                    RefreshCurrentStatus();
                }));
            }
        });
    }

    private void SetUiEnabledDuringOperation(bool enabled)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetUiEnabledDuringOperation(enabled)));
            return;
        }

        btnLevel1.Enabled = enabled;
        btnLevel2.Enabled = enabled;
        btnLevel3.Enabled = enabled;
        btnRefresh.Enabled = enabled;
        btnOpenLog.Enabled = enabled;
    }

    private void SetOperationStatus(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetOperationStatus(text)));
            return;
        }

        operationStatusLabel.Text = text;
        operationStatusLabel.Visible = !string.IsNullOrEmpty(text);
    }

    // Two-stage confirmation for Level 1 (simplified per user request)
    private bool ConfirmNuclearPurge()
    {
        var first = MessageBox.Show(
            "LEVEL 1 — FULL NUCLEAR / HARD PURGE\n\n" +
            "This operation will:\n" +
            "• Stop & disable the Program Compatibility Assistant (PcaSvc)\n" +
            "• Apply aggressive AppCompat policies (DisablePCA, AITEnable=0, etc.)\n" +
            "• Disable the four Application Experience scheduled tasks\n" +
            "• Delete all user + system compatibility layers from the registry\n" +
            "• Attempt to create a System Restore point\n" +
            "• Create a robust timestamped backup (AmCache.hve if present)\n\n" +
            "Most modern applications will be unaffected, but certain very old legacy software may break.\n\n" +
            "Continue only if you understand the risks and have important data backed up.",
            "LEVEL 1 — MAJOR WARNING",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (first != DialogResult.Yes) return false;

        var second = MessageBox.Show(
            "FINAL CONFIRMATION\n\n" +
            "You are about to execute the most aggressive option.\n\n" +
            "A System Restore point + full backup will be attempted automatically.\n\n" +
            "Are you absolutely sure you want to proceed with Level 1?",
            "LEVEL 1 — FINAL WARNING",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Stop);

        return second == DialogResult.Yes;
    }
}
