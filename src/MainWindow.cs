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
    /// <summary>Main application window with modern UI, system tray, monitoring and all dialogs.</summary>
    class MainWindow : Form
    {
        readonly AppSettings _settings = new AppSettings();
        FileMonitor _monitor;
        NotifyIcon _trayIcon;
        readonly List<ECUFormDialog> _activeDialogs = new List<ECUFormDialog>();

        // Navigation
        Button[] _navButtons;
        Panel[] _contentPanels;
        Panel _navIndicator;
        int _activeTab;

        // Monitor
        TextBox _monitorFolderInput, _destFolderInput;
        CheckBox _startupCheckbox, _openFolderCheckbox;
        Label _monitorStatusLabel;
        Button _startStopBtn;

        // Recent
        DataGridView _recentGrid;

        // Search
        TextBox _searchReg, _searchMake, _searchModel, _searchEcu;
        DataGridView _searchGrid;
        Label _searchCountLabel;
        readonly List<Dictionary<string, string>> _searchResults = new List<Dictionary<string, string>>();

        // History
        DataGridView _historyGrid;
        List<Dictionary<string, string>> _historyFolders = new List<Dictionary<string, string>>();

        // Status bar
        ToolStripStatusLabel _statusLabel, _versionLabel;

        // ====================================================================
        // Colors
        // ====================================================================

        static readonly Color PrimaryColor = Color.FromArgb(33, 150, 243);
        static readonly Color PrimaryDark = Color.FromArgb(21, 101, 192);
        static readonly Color AccentGreen = Color.FromArgb(76, 175, 80);
        static readonly Color AccentRed = Color.FromArgb(244, 67, 54);
        static readonly Color SurfaceColor = Color.FromArgb(250, 250, 250);
        static readonly Color BorderColor = Color.FromArgb(224, 224, 224);

        public AppSettings Settings => _settings;

        public MainWindow()
        {
            _settings.Load();
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            InitUI();
            SetupTray();
            RestoreGeometry();
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
                    return key?.GetValue(RunValueName) != null;
            }
            catch { return false; }
        }

        bool AddToStartup()
        {
            try
            {
                string exePath = $"\"{Application.ExecutablePath}\" --minimized";
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                    key.SetValue(RunValueName, exePath);
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
                    key?.DeleteValue(RunValueName, false);
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
        // UI Helpers
        // ====================================================================

        static Icon LoadAppIcon()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var stream = asm.GetManifestResourceStream("ECUFileOrganizer.Resources.app_icon.ico");
            return stream != null ? new Icon(stream) : SystemIcons.Application;
        }

        static Button CreateButton(string text, Color? bg = null, EventHandler click = null)
        {
            Color bgColor = bg ?? Color.White;
            Color fgColor = bg.HasValue ? Color.White : Color.FromArgb(60, 60, 60);

            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(10, 4, 10, 4),
                Cursor = Cursors.Hand,
                BackColor = bgColor,
                ForeColor = fgColor,
                Font = new Font("Segoe UI", 9f, bg.HasValue ? FontStyle.Bold : FontStyle.Regular)
            };

            if (bg.HasValue)
            {
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bg.Value, 0.15f);
                btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(bg.Value, 0.1f);
            }
            else
            {
                btn.FlatAppearance.BorderColor = BorderColor;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 245, 245);
                btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(230, 230, 230);
            }

            if (click != null) btn.Click += click;
            return btn;
        }

        static DataGridView CreateGrid(params string[] columns)
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = BorderColor,
                EnableHeadersVisualStyles = false,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ColumnHeadersHeight = 36,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = PrimaryColor;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 4, 8, 4);

            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(187, 222, 251);
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;
            grid.DefaultCellStyle.Padding = new Padding(8, 2, 8, 2);
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
            grid.RowTemplate.Height = 30;

            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);

            foreach (var col in columns)
                grid.Columns.Add(col.Replace(" ", ""), col);

            return grid;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, string lParam);

        // Custom color table for StatusStrip
        class StatusStripColors : ProfessionalColorTable
        {
            public override Color StatusStripGradientBegin => PrimaryDark;
            public override Color StatusStripGradientEnd => PrimaryDark;
        }

        // ====================================================================
        // Main UI
        // ====================================================================

        void InitUI()
        {
            SuspendLayout();

            Text = Constants.AppDisplayName;
            Font = new Font("Segoe UI", 9.5f);
            BackColor = SurfaceColor;
            MinimumSize = new Size(650, 550);
            Size = new Size(750, 620);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = LoadAppIcon();
            KeyPreview = true;
            AllowDrop = true;

            // --- Menu bar ---
            var menuBar = new MenuStrip { BackColor = Color.White, Padding = new Padding(4, 2, 0, 2) };

            var fileMenu = new ToolStripMenuItem("&File");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Monitor", null, (s, e) => SwitchTab(0))
                { ShortcutKeyDisplayString = "Ctrl+1" });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Recent Files", null, (s, e) => SwitchTab(1))
                { ShortcutKeyDisplayString = "Ctrl+2" });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Search", null, (s, e) => { SwitchTab(2); _searchReg?.Focus(); })
                { ShortcutKeyDisplayString = "Ctrl+3" });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&History", null, (s, e) => SwitchTab(3))
                { ShortcutKeyDisplayString = "Ctrl+4" });
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Minimize to &Tray", null, (s, e) => Hide());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("E&xit", null, (s, e) => QuitApplication())
                { ShortcutKeyDisplayString = "Ctrl+Q" });

            var helpMenu = new ToolStripMenuItem("&Help");
            helpMenu.DropDownItems.Add("&About", null, (s, e) => ShowAbout());
            helpMenu.DropDownItems.Add("Support the &Developers", null, (s, e) => ShowSupport());

            menuBar.Items.Add(fileMenu);
            menuBar.Items.Add(helpMenu);
            MainMenuStrip = menuBar;

            // --- Navigation bar ---
            var navBar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = PrimaryColor };
            string[] navLabels = { "Monitor", "Recent Files", "Search", "History" };
            _navButtons = new Button[4];

            var navFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(4, 0, 0, 0)
            };

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var btn = new Button
                {
                    Text = navLabels[i],
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(130, 40),
                    Margin = new Padding(1, 2, 1, 0),
                    ForeColor = Color.White,
                    BackColor = PrimaryColor,
                    Font = new Font("Segoe UI", 9.5f),
                    Cursor = Cursors.Hand,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(PrimaryColor, 0.12f);
                btn.FlatAppearance.MouseDownBackColor = PrimaryDark;
                btn.Click += (s, e) => SwitchTab(idx);
                _navButtons[i] = btn;
                navFlow.Controls.Add(btn);
            }

            navBar.Controls.Add(navFlow);

            // Bottom accent indicator
            _navIndicator = new Panel
            {
                Height = 3,
                BackColor = Color.White,
                Width = 130,
                Location = new Point(5, 41)
            };
            navBar.Controls.Add(_navIndicator);
            _navIndicator.BringToFront();

            // --- Status strip ---
            var statusStrip = new StatusStrip
            {
                Renderer = new ToolStripProfessionalRenderer(new StatusStripColors()),
                SizingGrip = true
            };
            _statusLabel = new ToolStripStatusLabel("Ready")
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White
            };
            _versionLabel = new ToolStripStatusLabel($"v{Constants.AppVersion}")
            {
                ForeColor = Color.FromArgb(180, 200, 255)
            };
            statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _versionLabel });

            // --- Content panels ---
            var contentContainer = new Panel { Dock = DockStyle.Fill, BackColor = SurfaceColor };
            _contentPanels = new Panel[4];
            for (int i = 0; i < 4; i++)
            {
                _contentPanels[i] = new Panel
                {
                    Dock = DockStyle.Fill,
                    Visible = false,
                    BackColor = SurfaceColor,
                    Padding = new Padding(15, 12, 15, 4)
                };
                contentContainer.Controls.Add(_contentPanels[i]);
            }

            SetupMonitorPanel(_contentPanels[0]);
            SetupRecentPanel(_contentPanels[1]);
            SetupSearchPanel(_contentPanels[2]);
            SetupHistoryPanel(_contentPanels[3]);

            // --- Add controls in correct docking order ---
            // WinForms docks from highest z-order (last added) to lowest (first added).
            // Fill must be FIRST (back/lowest z-order) so it gets remaining space.
            // Top controls: higher z-order = closer to top edge.
            Controls.Add(contentContainer); // Dock.Fill   – back, gets remaining space
            Controls.Add(navBar);           // Dock.Top    – below menuBar
            Controls.Add(statusStrip);      // Dock.Bottom – bottom edge
            Controls.Add(menuBar);          // Dock.Top    – front, very top edge

            ResumeLayout(true);

            // Activate first tab
            SwitchTab(0);

            // Drag & drop
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
        }

        void SwitchTab(int index)
        {
            _activeTab = index;
            for (int i = 0; i < 4; i++)
            {
                _contentPanels[i].Visible = (i == index);
                _navButtons[i].BackColor = (i == index) ? PrimaryDark : PrimaryColor;
                _navButtons[i].Font = new Font("Segoe UI", 9.5f,
                    i == index ? FontStyle.Bold : FontStyle.Regular);
            }

            // Move accent indicator under active button
            var btn = _navButtons[index];
            int x = btn.Parent.Padding.Left + btn.Left + btn.Margin.Left;
            _navIndicator.Location = new Point(x, _navIndicator.Parent.Height - 3);
            _navIndicator.Width = btn.Width;

            if (index == 1) RefreshRecentTab();
            if (index == 3) RefreshHistoryTab();
        }

        // ====================================================================
        // Monitor Panel
        // ====================================================================

        void SetupMonitorPanel(Panel panel)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                AutoSize = false
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // --- Settings group ---
            var settingsGroup = new GroupBox
            {
                Text = "Settings",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(8, 10, 8, 6)
            };
            var settingsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 4,
                AutoSize = true
            };
            settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            settingsTable.Controls.Add(new Label
            {
                Text = "Monitor Folder:",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true,
                Margin = new Padding(3, 6, 6, 3)
            }, 0, 0);
            _monitorFolderInput = new TextBox
            {
                Text = _settings.MonitorFolder,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            settingsTable.Controls.Add(_monitorFolderInput, 1, 0);
            settingsTable.Controls.Add(CreateButton("Browse", click: (s, e) => BrowseMonitorFolder()), 2, 0);

            settingsTable.Controls.Add(new Label
            {
                Text = "Destination Folder:",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true,
                Margin = new Padding(3, 6, 6, 3)
            }, 0, 1);
            _destFolderInput = new TextBox
            {
                Text = _settings.DestinationBase,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            settingsTable.Controls.Add(_destFolderInput, 1, 1);
            settingsTable.Controls.Add(CreateButton("Browse", click: (s, e) => BrowseDestFolder()), 2, 1);

            _startupCheckbox = new CheckBox
            {
                Text = "Run on Windows startup",
                Checked = IsInStartup(),
                AutoSize = true,
                Margin = new Padding(3, 6, 0, 0)
            };
            _startupCheckbox.CheckedChanged += (s, e) => ToggleStartup();
            settingsTable.SetColumnSpan(_startupCheckbox, 3);
            settingsTable.Controls.Add(_startupCheckbox, 0, 2);

            _openFolderCheckbox = new CheckBox
            {
                Text = "Open folder when file is saved",
                Checked = _settings.OpenFolderOnSave,
                AutoSize = true,
                Margin = new Padding(3, 3, 0, 3)
            };
            _openFolderCheckbox.CheckedChanged += (s, e) =>
            {
                _settings.OpenFolderOnSave = _openFolderCheckbox.Checked;
                _settings.Save();
            };
            settingsTable.SetColumnSpan(_openFolderCheckbox, 3);
            settingsTable.Controls.Add(_openFolderCheckbox, 0, 3);

            settingsGroup.Controls.Add(settingsTable);
            layout.Controls.Add(settingsGroup, 0, 0);

            // --- Status group ---
            var statusGroup = new GroupBox
            {
                Text = "Status",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(8, 8, 8, 6)
            };
            _monitorStatusLabel = new Label
            {
                Text = "Monitoring not started",
                Dock = DockStyle.Fill,
                AutoSize = true,
                BackColor = Color.FromArgb(240, 240, 240),
                Padding = new Padding(10, 8, 10, 8)
            };
            statusGroup.Controls.Add(_monitorStatusLabel);
            layout.Controls.Add(statusGroup, 0, 1);

            // --- Buttons ---
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Padding = new Padding(0, 6, 0, 6)
            };
            _startStopBtn = CreateButton("Start Monitoring", AccentGreen, (s, e) => ToggleMonitoring());
            btnPanel.Controls.Add(_startStopBtn);
            btnPanel.Controls.Add(CreateButton("Minimize to Tray", click: (s, e) => Hide()));
            btnPanel.Controls.Add(CreateButton("Exit", AccentRed, (s, e) => QuitApplication()));
            layout.Controls.Add(btnPanel, 0, 2);

            // --- Support link (bottom) ---
            var supportPanel = new Panel { Dock = DockStyle.Fill };
            var supportLabel = new LinkLabel
            {
                Text = "If you like this app, you can support us on: Buy Me a Coffee",
                TextAlign = ContentAlignment.BottomCenter,
                Dock = DockStyle.Bottom,
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 0)
            };
            int linkStart = "If you like this app, you can support us on: ".Length;
            supportLabel.Links.Add(linkStart, "Buy Me a Coffee".Length, Constants.SupportUrl);
            supportLabel.LinkClicked += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(e.Link.LinkData.ToString()) { UseShellExecute = true }); }
                catch { }
            };
            supportPanel.Controls.Add(supportLabel);
            layout.Controls.Add(supportPanel, 0, 3);

            panel.Controls.Add(layout);
        }

        // ====================================================================
        // Recent Files Panel
        // ====================================================================

        void SetupRecentPanel(Panel panel)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(new Label
            {
                Text = "Recently organized files. Double-click to open folder.",
                ForeColor = Color.FromArgb(117, 117, 117),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 0, 0, 6)
            }, 0, 0);

            _recentGrid = CreateGrid("Time", "Make", "Folder", "File");
            _recentGrid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _settings.RecentFiles.Count)
                {
                    string path = _settings.RecentFiles[e.RowIndex].ContainsKey("folder_path")
                        ? _settings.RecentFiles[e.RowIndex]["folder_path"]?.ToString() : "";
                    OpenFolder(path);
                }
            };
            layout.Controls.Add(_recentGrid, 0, 1);

            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Padding = new Padding(0, 6, 0, 0)
            };
            btnPanel.Controls.Add(CreateButton("Open Folder", AccentGreen, (s, e) =>
            {
                if (_recentGrid.CurrentRow == null) return;
                int idx = _recentGrid.CurrentRow.Index;
                if (idx >= 0 && idx < _settings.RecentFiles.Count)
                {
                    string path = _settings.RecentFiles[idx].ContainsKey("folder_path")
                        ? _settings.RecentFiles[idx]["folder_path"]?.ToString() : "";
                    OpenFolder(path);
                }
            }));
            btnPanel.Controls.Add(CreateButton("View Log", PrimaryColor, (s, e) =>
            {
                if (_recentGrid.CurrentRow == null) return;
                int idx = _recentGrid.CurrentRow.Index;
                if (idx >= 0 && idx < _settings.RecentFiles.Count)
                {
                    string path = _settings.RecentFiles[idx].ContainsKey("folder_path")
                        ? _settings.RecentFiles[idx]["folder_path"]?.ToString() : "";
                    OpenLog(path);
                }
            }));
            btnPanel.Controls.Add(CreateButton("Clear History", click: (s, e) =>
            {
                if (MessageBox.Show(this,
                    "Are you sure you want to clear all recent files history?",
                    "Clear History", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                    == DialogResult.Yes)
                {
                    _settings.RecentFiles.Clear();
                    _settings.Save();
                    RefreshRecentTab();
                }
            }));
            layout.Controls.Add(btnPanel, 0, 2);

            panel.Controls.Add(layout);
        }

        void RefreshRecentTab()
        {
            _recentGrid.Rows.Clear();
            foreach (var entry in _settings.RecentFiles)
            {
                string ts = entry.ContainsKey("timestamp") ? entry["timestamp"]?.ToString() : "";
                string make = entry.ContainsKey("make") ? entry["make"]?.ToString() : "";
                string folder = entry.ContainsKey("folder_name") ? entry["folder_name"]?.ToString() : "";
                string file = entry.ContainsKey("filename") ? entry["filename"]?.ToString() : "";
                _recentGrid.Rows.Add(ts, make, folder, file);
            }
            _statusLabel.Text = $"Recent files: {_settings.RecentFiles.Count}";
        }

        // ====================================================================
        // Search Panel
        // ====================================================================

        void SetupSearchPanel(Panel panel)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Search fields
            var searchGroup = new GroupBox
            {
                Text = "Search Filters",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(8, 8, 8, 6)
            };
            var searchTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                AutoSize = true
            };
            searchTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            searchTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _searchReg = AddSearchField(searchTable, 0, 0, "Registration:", "e.g., AB12345");
            _searchMake = AddSearchField(searchTable, 2, 0, "Make:", "e.g., Volkswagen");
            _searchModel = AddSearchField(searchTable, 0, 1, "Model:", "e.g., Golf");
            _searchEcu = AddSearchField(searchTable, 2, 1, "ECU Type:", "e.g., PCR2.1");

            searchGroup.Controls.Add(searchTable);
            layout.Controls.Add(searchGroup, 0, 0);

            // Search button
            var searchBtnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 4, 0, 4)
            };
            var searchBtn = CreateButton("Search", AccentGreen, (s, e) => PerformSearch());
            searchBtn.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            searchBtn.Padding = new Padding(20, 4, 20, 4);
            searchBtnPanel.Controls.Add(searchBtn);
            layout.Controls.Add(searchBtnPanel, 0, 1);

            // Results grid
            _searchGrid = CreateGrid("Make", "Folder Name");
            _searchGrid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _searchResults.Count)
                    OpenFolder(_searchResults[e.RowIndex]["folder_path"]);
            };
            layout.Controls.Add(_searchGrid, 0, 2);

            // Count label
            _searchCountLabel = new Label
            {
                Text = "Enter search criteria and click Search  (or press Enter)",
                ForeColor = Color.FromArgb(117, 117, 117),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 4, 0, 0)
            };
            layout.Controls.Add(_searchCountLabel, 0, 3);

            // Buttons
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Padding = new Padding(0, 4, 0, 0)
            };
            btnPanel.Controls.Add(CreateButton("Open Folder", PrimaryColor, (s, e) =>
            {
                if (_searchGrid.CurrentRow == null || _searchGrid.CurrentRow.Index < 0) return;
                int idx = _searchGrid.CurrentRow.Index;
                if (idx < _searchResults.Count)
                    OpenFolder(_searchResults[idx]["folder_path"]);
            }));
            btnPanel.Controls.Add(CreateButton("View Log", click: (s, e) =>
            {
                if (_searchGrid.CurrentRow == null || _searchGrid.CurrentRow.Index < 0) return;
                int idx = _searchGrid.CurrentRow.Index;
                if (idx < _searchResults.Count)
                    OpenLog(_searchResults[idx]["folder_path"]);
            }));
            layout.Controls.Add(btnPanel, 0, 4);

            panel.Controls.Add(layout);
        }

        TextBox AddSearchField(TableLayoutPanel table, int col, int row, string label, string placeholder)
        {
            table.Controls.Add(new Label
            {
                Text = label,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true,
                Margin = new Padding(3, 6, 6, 3)
            }, col, row);
            var tb = new TextBox { Dock = DockStyle.Fill, BackColor = Color.White };
            SendMessage(tb.Handle, 0x1501, 0, placeholder);
            tb.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { PerformSearch(); e.SuppressKeyPress = true; } };
            table.Controls.Add(tb, col + 1, row);
            return tb;
        }

        void PerformSearch()
        {
            _searchGrid.Rows.Clear();
            _searchResults.Clear();

            string searchReg = _searchReg.Text.Trim().ToLower();
            string searchMake = _searchMake.Text.Trim().ToLower();
            string searchModel = _searchModel.Text.Trim().ToLower();
            string searchEcu = _searchEcu.Text.Trim().ToLower();

            if (searchReg == "" && searchMake == "" && searchModel == "" && searchEcu == "")
            {
                _searchCountLabel.Text = "Please enter at least one search criterion";
                _searchCountLabel.ForeColor = Color.FromArgb(255, 152, 0);
                return;
            }

            string destBase = _settings.DestinationBase;
            if (string.IsNullOrEmpty(destBase) || !Directory.Exists(destBase))
            {
                _searchCountLabel.Text = "Destination folder not set or doesn't exist";
                _searchCountLabel.ForeColor = AccentRed;
                return;
            }

            _searchCountLabel.Text = "Searching...";
            _searchCountLabel.ForeColor = PrimaryColor;
            Application.DoEvents();

            try
            {
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
                            _searchResults.Add(new Dictionary<string, string>
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
                _searchCountLabel.Text = $"Search error: {ex.Message}";
                _searchCountLabel.ForeColor = AccentRed;
                return;
            }

            foreach (var r in _searchResults)
                _searchGrid.Rows.Add(r["make"], r["folder_name"]);

            if (_searchResults.Count > 0)
            {
                _searchGrid.ClearSelection();
                _searchGrid.Rows[0].Selected = true;
                _searchCountLabel.Text = $"Found {_searchResults.Count} matching folder(s)";
                _searchCountLabel.ForeColor = AccentGreen;
            }
            else
            {
                _searchCountLabel.Text = "No folders found matching your criteria";
                _searchCountLabel.ForeColor = AccentRed;
            }

            _statusLabel.Text = $"Search: {_searchResults.Count} result(s)";
        }

        // ====================================================================
        // History Panel
        // ====================================================================

        void SetupHistoryPanel(Panel panel)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(new Label
            {
                Text = "Organized folders (most recent first). Double-click to edit.",
                ForeColor = Color.FromArgb(117, 117, 117),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 0, 0, 6)
            }, 0, 0);

            _historyGrid = CreateGrid("Make", "Folder Name");
            _historyGrid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _historyFolders.Count)
                    EditSelectedFolder(_historyFolders[e.RowIndex]);
            };
            layout.Controls.Add(_historyGrid, 0, 1);

            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Padding = new Padding(0, 6, 0, 0)
            };
            btnPanel.Controls.Add(CreateButton("Edit Selected", PrimaryColor, (s, e) =>
            {
                if (_historyGrid.CurrentRow == null) return;
                int idx = _historyGrid.CurrentRow.Index;
                if (idx >= 0 && idx < _historyFolders.Count)
                    EditSelectedFolder(_historyFolders[idx]);
            }));
            btnPanel.Controls.Add(CreateButton("Open Folder", AccentGreen, (s, e) =>
            {
                if (_historyGrid.CurrentRow == null) return;
                int idx = _historyGrid.CurrentRow.Index;
                if (idx >= 0 && idx < _historyFolders.Count)
                    OpenFolder(_historyFolders[idx]["path"]);
            }));
            btnPanel.Controls.Add(CreateButton("Refresh", click: (s, e) => RefreshHistoryTab()));

            layout.Controls.Add(btnPanel, 0, 2);
            panel.Controls.Add(layout);
        }

        void RefreshHistoryTab()
        {
            _historyGrid.Rows.Clear();
            _historyFolders = GetOrganizedFolders();
            foreach (var f in _historyFolders.Take(50))
                _historyGrid.Rows.Add(f["make"], f["name"]);
            _statusLabel.Text = $"History: {Math.Min(_historyFolders.Count, 50)} of {_historyFolders.Count} folder(s)";
        }

        // ====================================================================
        // Folder data helpers
        // ====================================================================

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

            folders.Sort((a, b) => string.Compare(b["modified"], a["modified"], StringComparison.Ordinal));
            return folders;
        }

        Dictionary<string, string> ParseFolderName(string folderName)
        {
            string[] parts = folderName.Split('_');
            var data = new Dictionary<string, string>
            {
                ["make"] = "", ["model"] = "", ["date"] = "", ["ecu"] = "",
                ["sw_version"] = "", ["read_method"] = "", ["mileage"] = "", ["registration"] = ""
            };

            if (parts.Length < 3) return data;

            data["make"] = parts[0];

            if (parts.Last() != "" && !parts.Last().EndsWith("km"))
                data["registration"] = parts.Last();

            for (int i = parts.Length - 1; i >= 0; i--)
            {
                if (parts[i].EndsWith("km"))
                {
                    data["mileage"] = parts[i].Replace("km", "");
                    break;
                }
            }

            foreach (string part in parts)
            {
                if (part.StartsWith("SW") && part.Length > 2 && part.Substring(2).All(char.IsDigit))
                {
                    data["sw_version"] = part.Substring(2);
                    break;
                }
            }

            string[] readMethodKeywords = { "OBD", "Bench", "Boot", "Virtual" };
            foreach (string method in readMethodKeywords)
            {
                if (parts.Contains(method))
                {
                    data["read_method"] = method == "OBD" ? "Normal Read-OBD" : method;
                    break;
                }
            }

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

        // ====================================================================
        // Edit folder dialog (modal)
        // ====================================================================

        void EditSelectedFolder(Dictionary<string, string> folderInfo)
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
                MaximizeBox = false,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = SurfaceColor
            })
            {
                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    RowCount = 6,
                    ColumnCount = 1,
                    Padding = new Padding(12)
                };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                layout.Controls.Add(new Label
                {
                    Text = "Edit Folder Information",
                    Font = new Font("Segoe UI", 13, FontStyle.Bold),
                    ForeColor = PrimaryDark,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    Padding = new Padding(0, 0, 0, 6)
                }, 0, 0);

                layout.Controls.Add(new Label
                {
                    Text = $"Current location:\n{folderPath}",
                    ForeColor = Color.FromArgb(117, 117, 117),
                    BackColor = Color.White,
                    Padding = new Padding(10, 8, 10, 8),
                    Dock = DockStyle.Fill,
                    AutoSize = true
                }, 0, 1);

                // Form fields
                var formGroup = new GroupBox
                {
                    Text = "Edit Information",
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    Padding = new Padding(8, 8, 8, 6)
                };
                var formTable = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 8,
                    AutoSize = true
                };
                formTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                formTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                var makeInput = AddEditField(formTable, 0, "Make:", data["make"]);
                var modelInput = AddEditField(formTable, 1, "Model:", data["model"]);
                var dateInput = AddEditField(formTable, 2, "Date:", data["date"], "YYYYMMDD");
                var ecuInput = AddEditField(formTable, 3, "ECU Type:", data["ecu"]);
                var swInput = AddEditField(formTable, 4, "Software Version:", data["sw_version"], "e.g., 9978");

                formTable.Controls.Add(new Label
                {
                    Text = "Read Method:",
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoSize = true,
                    Margin = new Padding(3, 6, 6, 3)
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
                var previewGroup = new GroupBox
                {
                    Text = "New Folder Name Preview",
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    Padding = new Padding(8, 8, 8, 6)
                };
                var previewLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    BackColor = Color.White,
                    Padding = new Padding(8, 6, 8, 6)
                };
                previewGroup.Controls.Add(previewLabel);
                layout.Controls.Add(previewGroup, 0, 3);

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
                    AutoSize = true,
                    Padding = new Padding(0, 6, 0, 0)
                };
                btnPanel.Controls.Add(CreateButton("Save Changes", AccentGreen, (s, e) => SaveFolderEdit(
                    folderPath, folderInfo["make"],
                    makeInput.Text.Trim(), modelInput.Text.Trim(),
                    dateInput.Text.Trim(), ecuInput.Text.Trim(),
                    swInput.Text.Trim(), readMethodCombo.SelectedItem?.ToString() ?? "",
                    mileageInput.Text.Trim(), regInput.Text.Trim(), dlg)));
                btnPanel.Controls.Add(CreateButton("Cancel", click: (s, e) => dlg.Close()));

                layout.Controls.Add(btnPanel, 0, 5);
                dlg.Controls.Add(layout);
                dlg.ShowDialog(this);
            }
        }

        TextBox AddEditField(TableLayoutPanel table, int row, string label, string value, string placeholder = null)
        {
            table.Controls.Add(new Label
            {
                Text = label,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true,
                Margin = new Padding(3, 6, 6, 3)
            }, 0, row);
            var tb = new TextBox { Text = value, Dock = DockStyle.Fill, BackColor = Color.White };
            if (placeholder != null)
                SendMessage(tb.Handle, 0x1501, 0, placeholder);
            table.Controls.Add(tb, 1, row);
            return tb;
        }

        void SaveFolderEdit(string oldPath, string oldMake, string make, string model,
            string date, string ecu, string swVersion, string readMethod,
            string mileage, string registration, Form editDialog)
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
                    $"A folder with this name already exists:\n\n{newFolderName}\n\nPlease use different values.",
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
                Directory.CreateDirectory(Path.Combine(destBase, make));
                Directory.Move(oldPath, newPath);

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
                RefreshHistoryTab();
            }
            catch (Exception ex)
            {
                MessageBox.Show(editDialog, $"Failed to rename folder:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                if (InvokeRequired)
                    BeginInvoke(new Action(() => HandleNewFile(filePath)));
                else
                    HandleNewFile(filePath);
            };
            _monitor.Start();

            _monitorStatusLabel.Text = $"Monitoring: {_settings.MonitorFolder}";
            _monitorStatusLabel.BackColor = Color.FromArgb(212, 237, 218);
            _monitorStatusLabel.ForeColor = Color.FromArgb(21, 87, 36);
            _startStopBtn.Text = "Stop Monitoring";
            _startStopBtn.BackColor = AccentRed;
            _startStopBtn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(AccentRed, 0.15f);
            _startStopBtn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(AccentRed, 0.1f);

            _statusLabel.Text = "Monitoring active";
            _trayIcon.Text = "ECU File Organizer - Monitoring Active";
            ShowTrayMessage("ECU Organizer", "Monitoring started", ToolTipIcon.Info, 2000);
        }

        void StopMonitoring()
        {
            _monitor?.Stop();

            _monitorStatusLabel.Text = "Monitoring stopped";
            _monitorStatusLabel.BackColor = Color.FromArgb(240, 240, 240);
            _monitorStatusLabel.ForeColor = SystemColors.ControlText;
            _startStopBtn.Text = "Start Monitoring";
            _startStopBtn.BackColor = AccentGreen;
            _startStopBtn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(AccentGreen, 0.15f);
            _startStopBtn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(AccentGreen, 0.1f);

            _statusLabel.Text = "Monitoring stopped";
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
            _statusLabel.Text = $"File detected: {filename}";
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
            _statusLabel.Text = $"File saved: {Path.GetFileName(destPath)}";
            ShowTrayMessage("File Organized", $"File saved to:\n{destPath}", ToolTipIcon.Info, 3000);
        }

        // ====================================================================
        // Window close / quit / geometry
        // ====================================================================

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveGeometry();
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
            SaveGeometry();
            StopMonitoring();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            Application.Exit();
        }

        void SaveGeometry()
        {
            if (WindowState == FormWindowState.Normal)
            {
                _settings.WindowX = Left;
                _settings.WindowY = Top;
                _settings.WindowWidth = Width;
                _settings.WindowHeight = Height;
                _settings.Save();
            }
        }

        void RestoreGeometry()
        {
            if (_settings.WindowX >= 0 && _settings.WindowY >= 0)
            {
                var rect = new Rectangle(_settings.WindowX, _settings.WindowY,
                    _settings.WindowWidth, _settings.WindowHeight);
                if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(rect)))
                {
                    StartPosition = FormStartPosition.Manual;
                    Left = _settings.WindowX;
                    Top = _settings.WindowY;
                }
            }
            if (_settings.WindowWidth > 0 && _settings.WindowHeight > 0)
            {
                Width = _settings.WindowWidth;
                Height = _settings.WindowHeight;
            }
        }

        // ====================================================================
        // About / Support dialogs
        // ====================================================================

        void ShowAbout()
        {
            using (var dlg = new Form
            {
                Text = $"About {Constants.AppName}",
                Size = new Size(460, 480),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = SurfaceColor
            })
            {
                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 5,
                    Padding = new Padding(24, 18, 24, 18)
                };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                layout.Controls.Add(new Label
                {
                    Text = Constants.AppDisplayName,
                    Font = new Font("Segoe UI", 15, FontStyle.Bold),
                    ForeColor = PrimaryDark,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    Padding = new Padding(0, 0, 0, 4)
                }, 0, 0);

                layout.Controls.Add(new Label
                {
                    Text = "Automatic ECU file organization tool\nfor automotive diagnostics.",
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.FromArgb(117, 117, 117),
                    Dock = DockStyle.Fill,
                    Padding = new Padding(0, 0, 0, 8)
                }, 0, 1);

                layout.Controls.Add(new Label
                {
                    Text = "Features:\n"
                        + "  - Automatic file monitoring\n"
                        + "  - Smart filename parsing\n"
                        + "  - Read method tracking (OBD, Bench, Boot)\n"
                        + "  - Duplicate detection & intelligent handling\n"
                        + "  - Edit History - Fix mistakes after organizing\n"
                        + "  - Auto-open folder on save\n"
                        + "  - Automatic session Log.txt creation\n"
                        + "  - Search & Filter files\n"
                        + "  - Drag & Drop support\n"
                        + "  - Keyboard shortcuts",
                    Dock = DockStyle.Fill,
                    AutoSize = true
                }, 0, 2);

                var devLabel = new LinkLabel
                {
                    Text = "Developers: Autobyte Diagnostics, Sn0w3y\nYear: 2026\n\nMade for automotive professionals",
                    TextAlign = ContentAlignment.TopCenter,
                    Dock = DockStyle.Fill
                };
                devLabel.Links.Add(12, 20, "https://github.com/autobytediag");
                devLabel.Links.Add(34, 6, "https://github.com/Sn0w3y");
                devLabel.LinkClicked += (s, e) =>
                {
                    try { Process.Start(new ProcessStartInfo(e.Link.LinkData.ToString()) { UseShellExecute = true }); }
                    catch { }
                };
                layout.Controls.Add(devLabel, 0, 3);

                var okBtn = CreateButton("OK", PrimaryColor, (s, e) => dlg.Close());
                okBtn.Dock = DockStyle.Right;
                okBtn.Width = 90;
                dlg.AcceptButton = okBtn;
                layout.Controls.Add(okBtn, 0, 4);

                dlg.Controls.Add(layout);
                dlg.ShowDialog(this);
            }
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
                "Support the Developers", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);

            if (result == DialogResult.OK)
            {
                try { Process.Start(new ProcessStartInfo(Constants.SupportUrl) { UseShellExecute = true }); }
                catch { }
            }
        }

        // ====================================================================
        // Keyboard shortcuts
        // ====================================================================

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.D1: SwitchTab(0); return true;
                case Keys.Control | Keys.D2: SwitchTab(1); return true;
                case Keys.Control | Keys.D3: SwitchTab(2); _searchReg?.Focus(); return true;
                case Keys.Control | Keys.D4: SwitchTab(3); return true;
                case Keys.Control | Keys.M: ToggleMonitoring(); return true;
                case Keys.Control | Keys.Q: QuitApplication(); return true;
                case Keys.Control | Keys.F: SwitchTab(2); _searchReg?.Focus(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ====================================================================
        // Drag & Drop
        // ====================================================================

        void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        void OnDragDrop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null) return;

            string[] validExtensions = { ".bin", ".ori", ".mod", ".frf", ".hex", ".sgm" };
            int count = 0;
            foreach (var file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (Array.IndexOf(validExtensions, ext) >= 0 && File.Exists(file))
                {
                    HandleNewFile(file);
                    count++;
                }
            }

            if (count == 0)
                MessageBox.Show(this,
                    "No supported ECU files found.\n\nSupported formats: .bin, .ori, .mod, .frf, .hex, .sgm",
                    "No Valid Files", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                _statusLabel.Text = $"Dropped {count} file(s)";
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
