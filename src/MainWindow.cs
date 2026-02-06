using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ECUFileOrganizer
{
    /// <summary>Main application window with system tray, monitoring and all dialogs.</summary>
    class MainWindow : Form
    {
        readonly AppSettings _settings = new AppSettings();
        FileMonitor _monitor;
        NotifyIcon _trayIcon;
        readonly List<ECUFormDialog> _activeDialogs = new List<ECUFormDialog>();

        TextBox _monitorFolderInput, _destFolderInput;
        CheckBox _startupCheckbox, _openFolderCheckbox;
        Label _statusLabel;
        Button _startStopBtn;

        public AppSettings Settings => _settings;

        public MainWindow()
        {
            _settings.Load();
            InitUI();
            SetupTray();
            StartMonitoring();
        }

        // ====================================================================
        // Windows Registry startup
        // ====================================================================

        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValueName = "ECUFileOrganizer";

        bool IsInStartup()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    return key?.GetValue(RunValueName) != null;
                }
            }
            catch { return false; }
        }

        bool AddToStartup()
        {
            try
            {
                string exePath = $"\"{Application.ExecutablePath}\" --minimized";
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    key.SetValue(RunValueName, exePath);
                }
                _settings.RunOnStartup = true;
                _settings.Save();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to add to startup:\n{ex.Message}",
                    "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        bool RemoveFromStartup()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    key?.DeleteValue(RunValueName, false);
                }
                _settings.RunOnStartup = false;
                _settings.Save();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to remove from startup:\n{ex.Message}",
                    "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        // ====================================================================
        // Main UI
        // ====================================================================

        static Icon LoadAppIcon()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var stream = asm.GetManifestResourceStream("ECUFileOrganizer.Resources.app_icon.ico");
            return stream != null ? new Icon(stream) : SystemIcons.Application;
        }

        void InitUI()
        {
            Text = Constants.AppName;
            Size = new Size(620, 580);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = LoadAppIcon();

            // Menu bar
            var menuBar = new MenuStrip();
            var helpMenu = new ToolStripMenuItem("Help");

            helpMenu.DropDownItems.Add("Search Files", null, (s, e) => ShowSearchDialog());
            helpMenu.DropDownItems.Add("Recent Files", null, (s, e) => ShowRecentFiles());
            helpMenu.DropDownItems.Add("History", null, (s, e) => ShowEditHistory());
            helpMenu.DropDownItems.Add(new ToolStripSeparator());
            helpMenu.DropDownItems.Add("About", null, (s, e) => ShowAbout());
            helpMenu.DropDownItems.Add(new ToolStripSeparator());
            helpMenu.DropDownItems.Add("Support the Developer", null, (s, e) => ShowSupport());

            menuBar.Items.Add(helpMenu);
            MainMenuStrip = menuBar;
            Controls.Add(menuBar);

            // Main panel
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 5, 15, 15)
            };

            // Title
            var title = new Label
            {
                Text = "ECU File Organizer",
                Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 40
            };

            // Settings group
            var settingsGroup = new GroupBox
            {
                Text = "Settings",
                Dock = DockStyle.Top,
                Height = 160,
                Padding = new Padding(10, 5, 10, 10)
            };

            var settingsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 4,
                AutoSize = false
            };
            settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            // Monitor folder
            settingsTable.Controls.Add(new Label
            {
                Text = "Monitor Folder:",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true
            }, 0, 0);
            _monitorFolderInput = new TextBox
            {
                Text = _settings.MonitorFolder,
                ReadOnly = true,
                Dock = DockStyle.Fill
            };
            settingsTable.Controls.Add(_monitorFolderInput, 1, 0);
            var browseMonitor = new Button { Text = "Browse", AutoSize = true };
            browseMonitor.Click += (s, e) => BrowseMonitorFolder();
            settingsTable.Controls.Add(browseMonitor, 2, 0);

            // Destination folder
            settingsTable.Controls.Add(new Label
            {
                Text = "Destination Folder:",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true
            }, 0, 1);
            _destFolderInput = new TextBox
            {
                Text = _settings.DestinationBase,
                ReadOnly = true,
                Dock = DockStyle.Fill
            };
            settingsTable.Controls.Add(_destFolderInput, 1, 1);
            var browseDest = new Button { Text = "Browse", AutoSize = true };
            browseDest.Click += (s, e) => BrowseDestFolder();
            settingsTable.Controls.Add(browseDest, 2, 1);

            // Startup checkbox
            _startupCheckbox = new CheckBox
            {
                Text = "Run on Windows startup",
                Checked = IsInStartup(),
                AutoSize = true
            };
            _startupCheckbox.CheckedChanged += (s, e) => ToggleStartup();
            settingsTable.SetColumnSpan(_startupCheckbox, 3);
            settingsTable.Controls.Add(_startupCheckbox, 0, 2);

            // Open folder checkbox
            _openFolderCheckbox = new CheckBox
            {
                Text = "Open folder when file is saved",
                Checked = _settings.OpenFolderOnSave,
                AutoSize = true
            };
            _openFolderCheckbox.CheckedChanged += (s, e) =>
            {
                _settings.OpenFolderOnSave = _openFolderCheckbox.Checked;
                _settings.Save();
            };
            settingsTable.SetColumnSpan(_openFolderCheckbox, 3);
            settingsTable.Controls.Add(_openFolderCheckbox, 0, 3);

            settingsGroup.Controls.Add(settingsTable);

            // Status group
            var statusGroup = new GroupBox
            {
                Text = "Status",
                Dock = DockStyle.Top,
                Height = 60
            };
            _statusLabel = new Label
            {
                Text = "Monitoring not started",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 240, 240),
                Padding = new Padding(10)
            };
            statusGroup.Controls.Add(_statusLabel);

            // Buttons panel
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 5, 0, 5)
            };

            _startStopBtn = new Button { Text = "Start Monitoring", AutoSize = true };
            _startStopBtn.Click += (s, e) => ToggleMonitoring();
            btnPanel.Controls.Add(_startStopBtn);

            var minimizeBtn = new Button { Text = "Minimize to Tray", AutoSize = true };
            minimizeBtn.Click += (s, e) => Hide();
            btnPanel.Controls.Add(minimizeBtn);

            var historyBtn = new Button
            {
                Text = "History",
                AutoSize = true,
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Font, FontStyle.Bold)
            };
            historyBtn.Click += (s, e) => ShowEditHistory();
            btnPanel.Controls.Add(historyBtn);

            var exitBtn = new Button
            {
                Text = "Exit",
                AutoSize = true,
                BackColor = Color.FromArgb(244, 67, 54),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Font, FontStyle.Bold)
            };
            exitBtn.Click += (s, e) => QuitApplication();
            btnPanel.Controls.Add(exitBtn);

            // Support link
            var supportLabel = new LinkLabel
            {
                Text = "If you like this app, you can support me on: Buy Me a Coffee",
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 30,
                Padding = new Padding(10)
            };
            int linkStart = "If you like this app, you can support me on: ".Length;
            supportLabel.Links.Add(linkStart, "Buy Me a Coffee".Length, Constants.SupportUrl);
            supportLabel.LinkClicked += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(e.Link.LinkData.ToString()) { UseShellExecute = true }); }
                catch { }
            };

            // Add to main panel (top to bottom via Dock.Top, added in reverse)
            mainPanel.Controls.Add(supportLabel);
            mainPanel.Controls.Add(btnPanel);
            mainPanel.Controls.Add(statusGroup);
            mainPanel.Controls.Add(settingsGroup);
            mainPanel.Controls.Add(title);

            Controls.Add(mainPanel);
        }

        // ====================================================================
        // System tray
        // ====================================================================

        void SetupTray()
        {
            _trayIcon = new NotifyIcon
            {
                Text = Constants.AppName,
                Icon = Icon,
                Visible = true
            };

            var trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Show Window", null, (s, e) => { Show(); WindowState = FormWindowState.Normal; BringToFront(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Quit", null, (s, e) => QuitApplication());
            _trayIcon.ContextMenuStrip = trayMenu;

            _trayIcon.DoubleClick += (s, e) => { Show(); WindowState = FormWindowState.Normal; BringToFront(); };
        }

        public void ShowTrayMessage(string title, string text, ToolTipIcon icon, int timeout)
        {
            _trayIcon?.ShowBalloonTip(timeout, title, text, icon);
        }

        // ====================================================================
        // Folder browsing
        // ====================================================================

        void BrowseMonitorFolder()
        {
            using (var dlg = new FolderBrowserDialog
            {
                Description = "Select Monitor Folder",
                SelectedPath = _settings.MonitorFolder
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _monitorFolderInput.Text = dlg.SelectedPath;
                    _settings.MonitorFolder = dlg.SelectedPath;
                    _settings.Save();

                    if (_monitor != null && _monitor.IsRunning)
                    {
                        StopMonitoring();
                        StartMonitoring();
                    }
                }
            }
        }

        void BrowseDestFolder()
        {
            using (var dlg = new FolderBrowserDialog
            {
                Description = "Select Destination Folder",
                SelectedPath = _settings.DestinationBase
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _destFolderInput.Text = dlg.SelectedPath;
                    _settings.DestinationBase = dlg.SelectedPath;
                    _settings.Save();
                }
            }
        }

        // ====================================================================
        // Startup toggle
        // ====================================================================

        void ToggleStartup()
        {
            if (_startupCheckbox.Checked)
            {
                if (AddToStartup())
                    ShowTrayMessage("Startup Enabled",
                        "ECU Organizer will start with Windows (minimized to tray)",
                        ToolTipIcon.Info, 2000);
            }
            else
            {
                if (RemoveFromStartup())
                    ShowTrayMessage("Startup Disabled",
                        "ECU Organizer will not start with Windows",
                        ToolTipIcon.Info, 2000);
            }
        }

        // ====================================================================
        // File monitoring
        // ====================================================================

        void StartMonitoring()
        {
            try { Directory.CreateDirectory(_settings.DestinationBase); } catch { }

            _monitor = new FileMonitor(_settings.MonitorFolder);
            _monitor.FileDetected += filePath =>
            {
                // Thread-safe UI update
                if (InvokeRequired)
                    BeginInvoke(new Action(() => HandleNewFile(filePath)));
                else
                    HandleNewFile(filePath);
            };
            _monitor.Start();

            _statusLabel.Text = $"Monitoring: {_settings.MonitorFolder}";
            _statusLabel.BackColor = Color.FromArgb(212, 237, 218);
            _statusLabel.ForeColor = Color.FromArgb(21, 87, 36);
            _startStopBtn.Text = "Stop Monitoring";

            _trayIcon.Text = "ECU File Organizer - Monitoring Active";
            ShowTrayMessage("ECU Organizer", "Monitoring started", ToolTipIcon.Info, 2000);
        }

        void StopMonitoring()
        {
            _monitor?.Stop();

            _statusLabel.Text = "Monitoring stopped";
            _statusLabel.BackColor = Color.FromArgb(240, 240, 240);
            _statusLabel.ForeColor = SystemColors.ControlText;
            _startStopBtn.Text = "Start Monitoring";

            _trayIcon.Text = "ECU File Organizer - Monitoring Stopped";
        }

        void ToggleMonitoring()
        {
            if (_monitor != null && _monitor.IsRunning)
                StopMonitoring();
            else
                StartMonitoring();
        }

        void HandleNewFile(string filePath)
        {
            string filename = Path.GetFileName(filePath);
            var parsedData = FileParser.ParseBinFile(filePath);

            ShowFileForm(filePath, parsedData);

            ShowTrayMessage("New ECU File", $"File detected: {filename}", ToolTipIcon.Info, 3000);
        }

        void ShowFileForm(string filePath, Dictionary<string, string> parsedData)
        {
            var dialog = new ECUFormDialog(filePath, parsedData, _settings.DestinationBase, this);
            dialog.FileSaved += OnFileSaved;
            dialog.FormClosed += (s, e) => _activeDialogs.Remove(dialog);
            _activeDialogs.Add(dialog);
            dialog.Show();
        }

        void OnFileSaved(string destPath)
        {
            _settings.AddRecentFile(destPath);

            ShowTrayMessage("File Organized", $"File saved to:\n{destPath}", ToolTipIcon.Info, 3000);
        }

        // ====================================================================
        // Window close → minimize to tray
        // ====================================================================

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                ShowTrayMessage("ECU Organizer", "Application minimized to tray",
                    ToolTipIcon.Info, 2000);
                return;
            }
            base.OnFormClosing(e);
        }

        void QuitApplication()
        {
            StopMonitoring();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            Application.Exit();
        }

        // ====================================================================
        // Recent Files dialog
        // ====================================================================

        void ShowRecentFiles()
        {
            var recentFiles = _settings.RecentFiles;

            if (recentFiles.Count == 0)
            {
                MessageBox.Show(this,
                    "No files have been organized yet.\n\nOrganize some files first, then they will appear here.",
                    "No Recent Files", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new Form
            {
                Text = "Recent Files",
                Size = new Size(720, 520),
                StartPosition = FormStartPosition.CenterParent,
                MinimumSize = new Size(500, 400)
            })
            {
                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    RowCount = 4,
                    ColumnCount = 1,
                    Padding = new Padding(10)
                };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                var title = new Label
                {
                    Text = "Recent Files",
                    Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill
                };
                layout.Controls.Add(title, 0, 0);

                var info = new Label
                {
                    Text = $"Showing last {recentFiles.Count} organized files",
                    ForeColor = Color.Gray,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill
                };
                layout.Controls.Add(info, 0, 1);

                var filesList = new ListBox { Dock = DockStyle.Fill };
                foreach (var entry in recentFiles)
                {
                    string ts = entry.ContainsKey("timestamp") ? entry["timestamp"]?.ToString() : "";
                    string make = entry.ContainsKey("make") ? entry["make"]?.ToString() : "";
                    string folderName = entry.ContainsKey("folder_name") ? entry["folder_name"]?.ToString() : "";
                    filesList.Items.Add($"{ts} - {make} / {folderName}");
                }
                if (filesList.Items.Count > 0) filesList.SelectedIndex = 0;
                layout.Controls.Add(filesList, 0, 2);

                var btnPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.LeftToRight,
                    Height = 40
                };

                var openBtn = new Button
                {
                    Text = "Open Folder",
                    AutoSize = true,
                    BackColor = Color.FromArgb(76, 175, 80),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font(Font, FontStyle.Bold)
                };
                openBtn.Click += (s, e) =>
                {
                    if (filesList.SelectedIndex < 0) return;
                    string path = recentFiles[filesList.SelectedIndex].ContainsKey("folder_path")
                        ? recentFiles[filesList.SelectedIndex]["folder_path"]?.ToString() : "";
                    OpenFolder(path);
                };
                btnPanel.Controls.Add(openBtn);

                var logBtn = new Button
                {
                    Text = "View Log",
                    AutoSize = true,
                    BackColor = Color.FromArgb(33, 150, 243),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font(Font, FontStyle.Bold)
                };
                logBtn.Click += (s, e) =>
                {
                    if (filesList.SelectedIndex < 0) return;
                    string path = recentFiles[filesList.SelectedIndex].ContainsKey("folder_path")
                        ? recentFiles[filesList.SelectedIndex]["folder_path"]?.ToString() : "";
                    OpenLog(path);
                };
                btnPanel.Controls.Add(logBtn);

                // Spacer
                btnPanel.Controls.Add(new Label { AutoSize = false, Width = 200 });

                var clearBtn = new Button { Text = "Clear History", AutoSize = true };
                clearBtn.Click += (s, e) =>
                {
                    if (MessageBox.Show(dlg,
                        "Are you sure you want to clear all recent files history?",
                        "Clear History", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                        == DialogResult.Yes)
                    {
                        _settings.RecentFiles.Clear();
                        _settings.Save();
                        dlg.Close();
                        MessageBox.Show(this, "Recent files history has been cleared.",
                            "History Cleared", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };
                btnPanel.Controls.Add(clearBtn);

                var closeBtn = new Button { Text = "Close", AutoSize = true };
                closeBtn.Click += (s, e) => dlg.Close();
                btnPanel.Controls.Add(closeBtn);

                layout.Controls.Add(btnPanel, 0, 3);
                dlg.Controls.Add(layout);
                dlg.ShowDialog(this);
            }
        }

        // ====================================================================
        // Search & Filter dialog
        // ====================================================================

        void ShowSearchDialog()
        {
            string destBase = _settings.DestinationBase;
            if (string.IsNullOrEmpty(destBase) || !Directory.Exists(destBase))
            {
                MessageBox.Show(this,
                    "Please set the destination folder in Settings first.",
                    "No Destination Set", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dlg = new Form
            {
                Text = "Search & Filter Files",
                Size = new Size(820, 620),
                StartPosition = FormStartPosition.CenterParent,
                MinimumSize = new Size(600, 500)
            })
            {
                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    RowCount = 6,
                    ColumnCount = 1,
                    Padding = new Padding(10)
                };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // title
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // search group
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // search button
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // results list
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // count label
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // buttons

                var title = new Label
                {
                    Text = "Search & Filter Files",
                    Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill
                };
                layout.Controls.Add(title, 0, 0);

                // Search fields
                var searchGroup = new GroupBox { Text = "Search Filters", Dock = DockStyle.Fill, Height = 130 };
                var searchTable = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 4
                };
                searchTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                searchTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                var regInput = AddSearchField(searchTable, 0, "Registration:", "e.g., AB12345");
                var makeInput = AddSearchField(searchTable, 1, "Make:", "e.g., Volkswagen");
                var modelInput = AddSearchField(searchTable, 2, "Model:", "e.g., Golf");
                var ecuInput = AddSearchField(searchTable, 3, "ECU Type:", "e.g., PCR2.1");

                searchGroup.Controls.Add(searchTable);
                layout.Controls.Add(searchGroup, 0, 1);

                // Search button
                var searchBtn = new Button
                {
                    Text = "Search",
                    AutoSize = true,
                    BackColor = Color.FromArgb(76, 175, 80),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font(Font.FontFamily, 11, FontStyle.Bold),
                    Dock = DockStyle.Fill,
                    Height = 35
                };
                layout.Controls.Add(searchBtn, 0, 2);

                var resultsList = new ListBox { Dock = DockStyle.Fill };
                layout.Controls.Add(resultsList, 0, 3);

                var countLabel = new Label
                {
                    Text = "Enter search criteria and click Search",
                    ForeColor = Color.Gray,
                    Dock = DockStyle.Fill
                };
                layout.Controls.Add(countLabel, 0, 4);

                // Store search results for folder access
                var searchResults = new List<Dictionary<string, string>>();

                searchBtn.Click += (s, e) =>
                {
                    PerformSearch(regInput.Text, makeInput.Text, modelInput.Text, ecuInput.Text,
                        resultsList, countLabel, searchResults);
                };

                // Bottom buttons
                var btnPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.LeftToRight,
                    Height = 40
                };

                var openBtn = new Button
                {
                    Text = "Open Folder",
                    AutoSize = true,
                    BackColor = Color.FromArgb(33, 150, 243),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font(Font, FontStyle.Bold)
                };
                openBtn.Click += (s, e) =>
                {
                    if (resultsList.SelectedIndex < 0)
                    {
                        MessageBox.Show(dlg, "Please select a folder from the search results first.",
                            "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    OpenFolder(searchResults[resultsList.SelectedIndex]["folder_path"]);
                };
                btnPanel.Controls.Add(openBtn);

                var logBtn = new Button { Text = "View Log", AutoSize = true };
                logBtn.Click += (s, e) =>
                {
                    if (resultsList.SelectedIndex < 0)
                    {
                        MessageBox.Show(dlg, "Please select a folder from the search results first.",
                            "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    OpenLog(searchResults[resultsList.SelectedIndex]["folder_path"]);
                };
                btnPanel.Controls.Add(logBtn);

                btnPanel.Controls.Add(new Label { AutoSize = false, Width = 300 });

                var closeBtn = new Button { Text = "Close", AutoSize = true };
                closeBtn.Click += (s, e) => dlg.Close();
                btnPanel.Controls.Add(closeBtn);

                layout.Controls.Add(btnPanel, 0, 5);
                dlg.Controls.Add(layout);
                dlg.ShowDialog(this);
            }
        }

        TextBox AddSearchField(TableLayoutPanel table, int row, string label, string placeholder)
        {
            table.Controls.Add(new Label
            {
                Text = label,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true
            }, 0, row);
            var tb = new TextBox { Dock = DockStyle.Fill };
            SendMessage(tb.Handle, 0x1501, 0, placeholder);
            table.Controls.Add(tb, 1, row);
            return tb;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, string lParam);

        void PerformSearch(string registration, string make, string model, string ecu,
            ListBox resultsList, Label countLabel, List<Dictionary<string, string>> results)
        {
            resultsList.Items.Clear();
            results.Clear();

            string searchReg = registration.Trim().ToLower();
            string searchMake = make.Trim().ToLower();
            string searchModel = model.Trim().ToLower();
            string searchEcu = ecu.Trim().ToLower();

            if (searchReg == "" && searchMake == "" && searchModel == "" && searchEcu == "")
            {
                countLabel.Text = "Please enter at least one search criterion";
                countLabel.ForeColor = Color.FromArgb(255, 152, 0);
                return;
            }

            countLabel.Text = "Searching...";
            countLabel.ForeColor = Color.FromArgb(33, 150, 243);
            Application.DoEvents();

            try
            {
                string destBase = _settings.DestinationBase;
                foreach (string makeFolder in Directory.GetDirectories(destBase))
                {
                    string makeName = Path.GetFileName(makeFolder);
                    foreach (string folder in Directory.GetDirectories(makeFolder))
                    {
                        string folderName = Path.GetFileName(folder);
                        string folderLower = folderName.ToLower();

                        bool match = true;
                        if (searchReg != "" && !folderLower.Contains(searchReg)) match = false;
                        if (searchMake != "" && !folderLower.Contains(searchMake)) match = false;
                        if (searchModel != "" && !folderLower.Contains(searchModel)) match = false;
                        if (searchEcu != "" && !folderLower.Contains(searchEcu)) match = false;

                        if (match)
                        {
                            results.Add(new Dictionary<string, string>
                            {
                                ["folder_path"] = folder,
                                ["folder_name"] = folderName,
                                ["make"] = makeName
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                countLabel.Text = $"Search error: {ex.Message}";
                countLabel.ForeColor = Color.FromArgb(244, 67, 54);
                return;
            }

            if (results.Count > 0)
            {
                foreach (var r in results)
                    resultsList.Items.Add($"{r["make"]} / {r["folder_name"]}");
                resultsList.SelectedIndex = 0;
                countLabel.Text = $"Found {results.Count} matching folder(s)";
                countLabel.ForeColor = Color.FromArgb(76, 175, 80);
            }
            else
            {
                countLabel.Text = "No folders found matching your criteria";
                countLabel.ForeColor = Color.FromArgb(244, 67, 54);
            }
        }

        // ====================================================================
        // Edit History dialog
        // ====================================================================

        void ShowEditHistory()
        {
            try
            {
                string destBase = _settings.DestinationBase;
                if (string.IsNullOrEmpty(destBase))
                {
                    MessageBox.Show(this,
                        "Please set the destination folder in Settings first.\n\n"
                        + "The destination folder is where your organized files are stored.",
                        "No Destination Set", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var folders = GetOrganizedFolders();

                if (folders.Count == 0)
                {
                    MessageBox.Show(this,
                        "No organized folders found.\n\nOrganize some files first, then you can edit them here.",
                        "No History", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var dlg = new Form
                {
                    Text = "Edit Folder History",
                    Size = new Size(820, 620),
                    StartPosition = FormStartPosition.CenterParent,
                    MinimumSize = new Size(600, 400)
                })
                {
                    var layout = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        RowCount = 5,
                        ColumnCount = 1,
                        Padding = new Padding(10)
                    };
                    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                    var title = new Label
                    {
                        Text = "Edit Organized Folders",
                        Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
                        TextAlign = ContentAlignment.MiddleCenter,
                        Dock = DockStyle.Fill
                    };
                    layout.Controls.Add(title, 0, 0);

                    var info = new Label
                    {
                        Text = "Select a folder from the list below to edit its information.\n"
                            + "The folder will be renamed based on your changes.",
                        ForeColor = Color.Gray,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Dock = DockStyle.Fill
                    };
                    layout.Controls.Add(info, 0, 1);

                    var listLabel = new Label
                    {
                        Text = "Recent Folders (max 50):",
                        Font = new Font(Font, FontStyle.Bold),
                        Dock = DockStyle.Fill
                    };
                    layout.Controls.Add(listLabel, 0, 2);

                    var folderList = new ListBox { Dock = DockStyle.Fill };
                    foreach (var f in folders.Take(50))
                        folderList.Items.Add($"{f["make"]} / {f["name"]}");
                    if (folderList.Items.Count > 0) folderList.SelectedIndex = 0;
                    layout.Controls.Add(folderList, 0, 3);

                    var btnPanel = new FlowLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        FlowDirection = FlowDirection.LeftToRight,
                        Height = 40
                    };

                    var editBtn = new Button
                    {
                        Text = "Edit Selected Folder",
                        AutoSize = true,
                        BackColor = Color.FromArgb(33, 150, 243),
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Font = new Font(Font, FontStyle.Bold)
                    };
                    editBtn.Click += (s, e) =>
                    {
                        int idx = folderList.SelectedIndex;
                        if (idx >= 0 && idx < folders.Count)
                            EditSelectedFolder(folders[idx], dlg);
                    };
                    btnPanel.Controls.Add(editBtn);

                    btnPanel.Controls.Add(new Label { AutoSize = false, Width = 300 });

                    var closeBtn = new Button { Text = "Close", AutoSize = true };
                    closeBtn.Click += (s, e) => dlg.Close();
                    btnPanel.Controls.Add(closeBtn);

                    layout.Controls.Add(btnPanel, 0, 4);
                    dlg.Controls.Add(layout);
                    dlg.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"Failed to show history:\n\n{ex.Message}\n\nError type: {ex.GetType().Name}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        List<Dictionary<string, string>> GetOrganizedFolders()
        {
            var folders = new List<Dictionary<string, string>>();
            string destBase = _settings.DestinationBase;

            if (string.IsNullOrEmpty(destBase) || !Directory.Exists(destBase))
                return folders;

            try
            {
                foreach (string makeFolder in Directory.GetDirectories(destBase))
                {
                    string makeName = Path.GetFileName(makeFolder);
                    foreach (string folder in Directory.GetDirectories(makeFolder))
                    {
                        string folderName = Path.GetFileName(folder);
                        double modTime = 0;
                        try { modTime = new FileInfo(folder).LastWriteTime.Ticks; } catch { }

                        folders.Add(new Dictionary<string, string>
                        {
                            ["path"] = folder,
                            ["name"] = folderName,
                            ["make"] = makeName,
                            ["modified"] = modTime.ToString()
                        });
                    }
                }
            }
            catch { }

            // Sort by modification time (newest first)
            folders.Sort((a, b) => string.Compare(b["modified"], a["modified"], StringComparison.Ordinal));
            return folders;
        }

        Dictionary<string, string> ParseFolderName(string folderName)
        {
            string[] parts = folderName.Split('_');
            var data = new Dictionary<string, string>
            {
                ["make"] = "",
                ["model"] = "",
                ["date"] = "",
                ["ecu"] = "",
                ["sw_version"] = "",
                ["read_method"] = "",
                ["mileage"] = "",
                ["registration"] = ""
            };

            if (parts.Length < 3) return data;

            data["make"] = parts[0];

            // Registration (last part, if doesn't end with km)
            if (parts.Last() != "" && !parts.Last().EndsWith("km"))
                data["registration"] = parts.Last();

            // Mileage (part ending with 'km')
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                if (parts[i].EndsWith("km"))
                {
                    data["mileage"] = parts[i].Replace("km", "");
                    break;
                }
            }

            // SW Version (part starting with 'SW' followed by digits)
            foreach (string part in parts)
            {
                if (part.StartsWith("SW") && part.Length > 2 && part.Substring(2).All(char.IsDigit))
                {
                    data["sw_version"] = part.Substring(2);
                    break;
                }
            }

            // Read method
            string[] readMethodKeywords = { "OBD", "Bench", "Boot", "Virtual" };
            foreach (string method in readMethodKeywords)
            {
                if (parts.Contains(method))
                {
                    data["read_method"] = method == "OBD" ? "Normal Read-OBD" : method;
                    break;
                }
            }

            // Date + model + ECU
            var modelParts = new List<string>();
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].Length == 8 && parts[i].All(char.IsDigit))
                {
                    data["date"] = parts[i];
                    data["model"] = string.Join("_", modelParts);

                    var ecuParts = new List<string>();
                    for (int j = i + 1; j < parts.Length; j++)
                    {
                        if (readMethodKeywords.Contains(parts[j])
                            || parts[j].EndsWith("km")
                            || (parts[j].StartsWith("SW") && parts[j].Length > 2
                                && parts[j].Substring(2).All(char.IsDigit)))
                            break;
                        ecuParts.Add(parts[j]);
                    }
                    data["ecu"] = string.Join("_", ecuParts);
                    break;
                }
                modelParts.Add(parts[i]);
            }

            return data;
        }

        void EditSelectedFolder(Dictionary<string, string> folderInfo, Form parentDialog)
        {
            string folderPath = folderInfo["path"];
            string folderName = folderInfo["name"];

            var data = ParseFolderName(folderName);

            using (var dlg = new Form
            {
                Text = $"Edit Folder: {folderName}",
                Size = new Size(620, 600),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false
            })
            {
                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    RowCount = 6,
                    ColumnCount = 1,
                    Padding = new Padding(10)
                };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // title
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // path
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // form group
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // preview
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // spacer
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // buttons

                var title = new Label
                {
                    Text = "Edit Folder Information",
                    Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill
                };
                layout.Controls.Add(title, 0, 0);

                var pathLabel = new Label
                {
                    Text = $"Current location:\n{folderPath}",
                    ForeColor = Color.Gray,
                    BackColor = Color.FromArgb(240, 240, 240),
                    Padding = new Padding(10),
                    Dock = DockStyle.Fill,
                    AutoSize = true
                };
                layout.Controls.Add(pathLabel, 0, 1);

                // Form fields
                var formGroup = new GroupBox { Text = "Edit Information", Dock = DockStyle.Fill, Height = 250 };
                var formTable = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 8
                };
                formTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                formTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                var makeInput = AddEditField(formTable, 0, "Make:", data["make"]);
                var modelInput = AddEditField(formTable, 1, "Model:", data["model"]);
                var dateInput = AddEditField(formTable, 2, "Date:", data["date"], "YYYYMMDD");
                var ecuInput = AddEditField(formTable, 3, "ECU Type:", data["ecu"]);
                var swInput = AddEditField(formTable, 4, "Software Version:", data["sw_version"], "e.g., 9978");

                // Read method combo
                formTable.Controls.Add(new Label
                {
                    Text = "Read Method:",
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoSize = true
                }, 0, 5);
                var readMethodCombo = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                readMethodCombo.Items.AddRange(Constants.ReadMethods);
                if (data["read_method"] != "")
                {
                    int idx = readMethodCombo.FindString(data["read_method"]);
                    if (idx >= 0) readMethodCombo.SelectedIndex = idx;
                }
                else
                {
                    readMethodCombo.SelectedIndex = 0;
                }
                formTable.Controls.Add(readMethodCombo, 1, 5);

                var mileageInput = AddEditField(formTable, 6, "Mileage (km):", data["mileage"], "e.g., 45000");
                var regInput = AddEditField(formTable, 7, "Registration No:", data["registration"], "e.g., AB12345");

                formGroup.Controls.Add(formTable);
                layout.Controls.Add(formGroup, 0, 2);

                // Preview
                var previewGroup = new GroupBox { Text = "New Folder Name Preview", Dock = DockStyle.Fill, Height = 50 };
                var previewLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(240, 240, 240),
                    Padding = new Padding(5)
                };
                previewGroup.Controls.Add(previewLabel);
                layout.Controls.Add(previewGroup, 0, 3);

                // Update preview
                Action updatePreview = () =>
                {
                    string mk = makeInput.Text.Trim();
                    string md = modelInput.Text.Trim().Replace(' ', '_');
                    string dt = dateInput.Text.Trim();
                    string ec = ecuInput.Text.Trim().Replace(' ', '_');
                    string sw = swInput.Text.Trim();
                    string rm = readMethodCombo.SelectedItem?.ToString() ?? "";
                    string ml = mileageInput.Text.Trim();
                    string rg = regInput.Text.Trim().Replace(' ', '_');

                    string rmShort = rm.Replace("Normal Read-", "").Replace("Virtual Read-", "Virtual");

                    if (mk != "" && md != "")
                    {
                        string name = $"{mk}_{md}_{dt}_{ec}";
                        if (sw != "") name += $"_SW{sw}";
                        name += $"_{rmShort}";
                        if (ml != "") name += $"_{ml}km";
                        if (rg != "") name += $"_{rg}";
                        previewLabel.Text = name;
                    }
                    else
                    {
                        previewLabel.Text = "Please fill in Make and Model";
                    }
                };

                makeInput.TextChanged += (s, e) => updatePreview();
                modelInput.TextChanged += (s, e) => updatePreview();
                dateInput.TextChanged += (s, e) => updatePreview();
                ecuInput.TextChanged += (s, e) => updatePreview();
                swInput.TextChanged += (s, e) => updatePreview();
                readMethodCombo.SelectedIndexChanged += (s, e) => updatePreview();
                mileageInput.TextChanged += (s, e) => updatePreview();
                regInput.TextChanged += (s, e) => updatePreview();
                updatePreview();

                // Buttons
                var btnPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    Height = 40
                };

                var saveBtn = new Button
                {
                    Text = "Save Changes",
                    AutoSize = true,
                    BackColor = Color.FromArgb(76, 175, 80),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font(Font, FontStyle.Bold)
                };
                saveBtn.Click += (s, e) => SaveFolderEdit(
                    folderPath, folderInfo["make"],
                    makeInput.Text.Trim(), modelInput.Text.Trim(),
                    dateInput.Text.Trim(), ecuInput.Text.Trim(),
                    swInput.Text.Trim(), readMethodCombo.SelectedItem?.ToString() ?? "",
                    mileageInput.Text.Trim(), regInput.Text.Trim(),
                    dlg, parentDialog);
                btnPanel.Controls.Add(saveBtn);

                var cancelBtn = new Button { Text = "Cancel", AutoSize = true };
                cancelBtn.Click += (s, e) => dlg.Close();
                btnPanel.Controls.Add(cancelBtn);

                layout.Controls.Add(btnPanel, 0, 5);
                dlg.Controls.Add(layout);
                dlg.ShowDialog(parentDialog);
            }
        }

        TextBox AddEditField(TableLayoutPanel table, int row, string label, string value, string placeholder = null)
        {
            table.Controls.Add(new Label
            {
                Text = label,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true
            }, 0, row);
            var tb = new TextBox { Text = value, Dock = DockStyle.Fill };
            if (placeholder != null)
                SendMessage(tb.Handle, 0x1501, 0, placeholder);
            table.Controls.Add(tb, 1, row);
            return tb;
        }

        void SaveFolderEdit(string oldPath, string oldMake, string make, string model,
            string date, string ecu, string swVersion, string readMethod,
            string mileage, string registration, Form editDialog, Form parentDialog)
        {
            if (make == "" || model == "")
            {
                MessageBox.Show(editDialog, "Please fill in at least Make and Model!",
                    "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            model = model.Replace(' ', '_');
            ecu = ecu.Replace(' ', '_');
            registration = registration.Replace(' ', '_');
            string rmShort = readMethod.Replace("Normal Read-", "").Replace("Virtual Read-", "Virtual");

            string newFolderName = $"{make}_{model}_{date}_{ecu}";
            if (swVersion != "") newFolderName += $"_SW{swVersion}";
            newFolderName += $"_{rmShort}";
            if (mileage != "") newFolderName += $"_{mileage}km";
            if (registration != "") newFolderName += $"_{registration}";

            string destBase = _settings.DestinationBase;
            string newPath = Path.Combine(destBase, make, newFolderName);

            if (oldPath == newPath)
            {
                MessageBox.Show(editDialog, "No changes detected. Folder name is the same.",
                    "No Changes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (Directory.Exists(newPath))
            {
                MessageBox.Show(editDialog,
                    $"A folder with this name already exists:\n\n{newFolderName}\n\n"
                    + "Please use different values.",
                    "Folder Exists", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show(editDialog,
                $"Rename folder?\n\nFrom: {Path.GetFileName(oldPath)}\nTo: {newFolderName}\n\n"
                + "This will rename the folder and may move it to a different make folder if you changed the make.",
                "Confirm Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                != DialogResult.Yes)
                return;

            try
            {
                string newMakeFolder = Path.Combine(destBase, make);
                Directory.CreateDirectory(newMakeFolder);

                Directory.Move(oldPath, newPath);

                // Clean up old make folder if empty
                string oldMakeFolder = Path.GetDirectoryName(oldPath);
                try
                {
                    if (Directory.GetFileSystemEntries(oldMakeFolder).Length == 0)
                        Directory.Delete(oldMakeFolder);
                }
                catch { }

                MessageBox.Show(editDialog,
                    $"Folder renamed successfully!\n\nNew location:\n{newPath}",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                editDialog.Close();
                parentDialog.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(editDialog,
                    $"Failed to rename folder:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ====================================================================
        // About / Support dialogs
        // ====================================================================

        void ShowAbout()
        {
            MessageBox.Show(this,
                $"{Constants.AppDisplayName}\n\n"
                + "Automatic ECU file organization tool for automotive diagnostics.\n\n"
                + "Features:\n"
                + "  - Automatic file monitoring\n"
                + "  - Smart filename parsing\n"
                + "  - Read method tracking (OBD, Bench, Boot)\n"
                + "  - Duplicate detection & intelligent handling\n"
                + "  - Edit History - Fix mistakes after organizing\n"
                + "  - Auto-open folder on save\n"
                + "  - Automatic session Log.txt creation\n"
                + "  - Search & Filter files\n"
                + "  - Recent Files list\n"
                + "  - Professional file organization\n\n"
                + "Developer: Autobyte Diagnostics\n"
                + "Year: 2026\n\n"
                + "Made for automotive professionals",
                $"About {Constants.AppName}", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void ShowSupport()
        {
            var result = MessageBox.Show(this,
                "Support This Project\n\n"
                + "If you find this app useful and it makes your work easier, "
                + "consider supporting its development!\n\n"
                + "Your support helps:\n"
                + "  - Keep the app free and updated\n"
                + "  - Add new features you request\n"
                + "  - Improve documentation\n"
                + "  - Develop more professional tools\n\n"
                + "Click OK to open Buy Me a Coffee\n\n"
                + "Thank you for your support!",
                "Support the Developer", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);

            if (result == DialogResult.OK)
            {
                try { Process.Start(new ProcessStartInfo(Constants.SupportUrl) { UseShellExecute = true }); }
                catch { }
            }
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        void OpenFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;

            if (Directory.Exists(folderPath))
            {
                try { Process.Start("explorer.exe", folderPath); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Could not open folder:\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                MessageBox.Show(this, $"The folder no longer exists:\n\n{folderPath}",
                    "Folder Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void OpenLog(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;

            string logPath = Path.Combine(folderPath, "Log.txt");
            if (File.Exists(logPath))
            {
                try { Process.Start(new ProcessStartInfo(logPath) { UseShellExecute = true }); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Could not open log file:\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                MessageBox.Show(this, $"Log.txt not found in:\n\n{folderPath}",
                    "Log Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
