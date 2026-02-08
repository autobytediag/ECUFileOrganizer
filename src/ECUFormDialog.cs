using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ECUFileOrganizer
{
    /// <summary>Pop-up form dialog for ECU file information entry.</summary>
    class ECUFormDialog : Form
    {
        readonly string _filePath;
        readonly string _destinationBase;
        readonly MainWindow _parentWindow;

        TextBox _makeInput, _modelInput, _dateInput, _ecuInput;
        TextBox _swVersionInput, _boschSwInput, _oemHwInput, _oemSwInput;
        TextBox _engineCodeInput, _engineTypeInput;
        ComboBox _readMethodCombo;
        TextBox _mileageInput, _registrationInput;
        Label _previewLabel;

        public event Action<string> FileSaved;

        public ECUFormDialog(string filePath, Dictionary<string, string> parsedData,
                             string destinationBase, MainWindow parentWindow)
        {
            _filePath = filePath;
            _destinationBase = destinationBase;
            _parentWindow = parentWindow;
            InitUI(parsedData);
        }

        void InitUI(Dictionary<string, string> d)
        {
            Text = "New ECU File Detected";
            TopMost = true;
            MinimumSize = new Size(520, 700);
            AutoSize = true;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            KeyPreview = true;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AutoSize = true,
                Padding = new Padding(10)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int row = 0;

            // Title
            var title = new Label
            {
                Text = "New ECU File Detected",
                Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            layout.SetColumnSpan(title, 2);
            layout.Controls.Add(title, 0, row++);

            // File info
            var fileLabel = new Label
            {
                Text = $"File: {Path.GetFileName(_filePath)}",
                ForeColor = Color.Gray,
                Dock = DockStyle.Fill
            };
            layout.SetColumnSpan(fileLabel, 2);
            layout.Controls.Add(fileLabel, 0, row++);

            // Form fields
            _makeInput = AddField(layout, ref row, "Make:", d["make"]);
            _modelInput = AddField(layout, ref row, "Model:", d["model"]);
            _dateInput = AddField(layout, ref row, "Date:", d["date"], "YYYYMMDD");
            _ecuInput = AddField(layout, ref row, "ECU Type:", d["ecu"]);
            _swVersionInput = AddField(layout, ref row, "Software Version:",
                d.ContainsKey("sw_version") ? d["sw_version"] : "", "e.g., 9978 (auto-detected from BIN)");
            _boschSwInput = AddField(layout, ref row, "Bosch SW Nr:",
                d.ContainsKey("bosch_sw_number") ? d["bosch_sw_number"] : "", "e.g., 1037563106 (auto-detected)");
            _oemHwInput = AddField(layout, ref row, "OEM HW Nr:",
                d.ContainsKey("oem_hw_number") ? d["oem_hw_number"] : "", "e.g., 03L907309AE (auto-detected)");
            _oemSwInput = AddField(layout, ref row, "OEM SW Nr:",
                d.ContainsKey("oem_sw_number") ? d["oem_sw_number"] : "", "e.g., 03L906018LE (auto-detected)");
            _engineCodeInput = AddField(layout, ref row, "Engine Code:",
                d.ContainsKey("engine_code") ? d["engine_code"] : "", "e.g., CFFB (auto-detected)");
            _engineTypeInput = AddField(layout, ref row, "Engine Type:",
                d.ContainsKey("engine_type") ? d["engine_type"] : "", "e.g., R4 2.0l TDI (auto-detected)");

            // Read Method dropdown
            layout.Controls.Add(new Label { Text = "Read Method:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            _readMethodCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _readMethodCombo.Items.AddRange(Constants.ReadMethods);
            if (d["read_method"] != "")
            {
                int idx = _readMethodCombo.FindString(d["read_method"]);
                if (idx >= 0) _readMethodCombo.SelectedIndex = idx;
            }
            else
            {
                _readMethodCombo.SelectedIndex = 0;
            }
            _readMethodCombo.SelectedIndexChanged += (s, e) => UpdatePreview();
            layout.Controls.Add(_readMethodCombo, 1, row++);

            _mileageInput = AddField(layout, ref row, "Mileage (km):", d["mileage"], "e.g., 45000");
            _registrationInput = AddField(layout, ref row, "Registration No:", d["registration"], "e.g., AB12345");

            // Preview
            var previewGroup = new GroupBox { Text = "Destination Preview", Dock = DockStyle.Fill };
            _previewLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 240, 240),
                Padding = new Padding(5)
            };
            previewGroup.Controls.Add(_previewLabel);
            layout.SetColumnSpan(previewGroup, 2);
            layout.Controls.Add(previewGroup, 0, row++);

            // Buttons
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true
            };

            var saveBtn = new Button
            {
                Text = "Save && Organize  (Ctrl+S)",
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Font, FontStyle.Bold),
                Padding = new Padding(8, 4, 8, 4),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += (s, e) => SaveFile();
            btnPanel.Controls.Add(saveBtn);

            var cancelBtn = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Padding = new Padding(8, 4, 8, 4)
            };
            cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            cancelBtn.Click += (s, e) => Close();
            btnPanel.Controls.Add(cancelBtn);

            layout.SetColumnSpan(btnPanel, 2);
            layout.Controls.Add(btnPanel, 0, row++);

            Controls.Add(layout);
            _mileageInput.Focus();
            UpdatePreview();
        }

        TextBox AddField(TableLayoutPanel layout, ref int row, string label,
                          string value, string placeholder = null)
        {
            layout.Controls.Add(new Label { Text = label, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            var tb = new TextBox { Text = value, Dock = DockStyle.Fill };
            if (placeholder != null)
            {
                // WinForms doesn't have built-in placeholder, use cue banner
                SendMessage(tb.Handle, 0x1501, 0, placeholder);
            }
            tb.TextChanged += (s, e) => UpdatePreview();
            layout.Controls.Add(tb, 1, row++);
            return tb;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, string lParam);

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                SaveFile();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void UpdatePreview()
        {
            string make = _makeInput.Text.Trim();
            string model = _modelInput.Text.Trim().Replace(' ', '_');
            string date = _dateInput.Text.Trim();
            string ecu = _ecuInput.Text.Trim().Replace(' ', '_');
            string swVer = _swVersionInput.Text.Trim();
            string readMethod = _readMethodCombo.SelectedItem?.ToString() ?? "";
            string mileage = _mileageInput.Text.Trim();
            string reg = _registrationInput.Text.Trim().Replace(' ', '_');

            string rmShort = readMethod.Replace("Normal Read-", "").Replace("Virtual Read-", "Virtual");

            if (make != "" && model != "")
            {
                string folder = $"{make}_{model}_{date}_{ecu}";
                if (swVer != "") folder += $"_SW{swVer}";
                folder += $"_{rmShort}";
                if (mileage != "") folder += $"_{mileage}km";
                if (reg != "") folder += $"_{reg}";

                _previewLabel.Text = Path.Combine(_destinationBase, make, folder);
            }
            else
            {
                _previewLabel.Text = "Please fill in Make and Model";
            }
        }

        List<Dictionary<string, string>> CheckExistingFolders(string registration)
        {
            var existing = new List<Dictionary<string, string>>();
            if (string.IsNullOrWhiteSpace(registration) || !Directory.Exists(_destinationBase))
                return existing;

            string regClean = registration.Trim().Replace(' ', '_');

            try
            {
                foreach (string makeFolder in Directory.GetDirectories(_destinationBase))
                {
                    foreach (string folder in Directory.GetDirectories(makeFolder))
                    {
                        string folderName = Path.GetFileName(folder);
                        if (folderName.EndsWith($"_{regClean}"))
                        {
                            existing.Add(new Dictionary<string, string>
                            {
                                ["path"] = folder,
                                ["name"] = folderName
                            });
                        }
                    }
                }
            }
            catch { }

            existing.Sort((a, b) => string.Compare(b["name"], a["name"], StringComparison.Ordinal));
            return existing;
        }

        string ShowDuplicateDialog(List<Dictionary<string, string>> existingFolders)
        {
            string reg = existingFolders[0]["name"].Split('_').Last();

            string text = $"Found {existingFolders.Count} existing folder(s) for registration: {reg}\n\n"
                + "Existing folders:\n";
            foreach (var f in existingFolders.Take(5))
                text += $"  - {f["name"]}\n";
            if (existingFolders.Count > 5)
                text += $"  ... and {existingFolders.Count - 5} more\n";
            text += "\nAdd to existing: Keep all files together\nCreate new: Separate by date/service";

            // Custom dialog with proper button labels:
            using (var dlg = new Form
            {
                Text = "Duplicate Detected",
                Size = new Size(500, 350),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            })
            {
                var lbl = new Label
                {
                    Text = text,
                    Dock = DockStyle.Fill,
                    Padding = new Padding(15)
                };
                dlg.Controls.Add(lbl);

                var btnPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    FlowDirection = FlowDirection.LeftToRight,
                    Height = 45,
                    Padding = new Padding(10, 5, 10, 5)
                };

                string choice = "cancel";

                var addBtn = new Button { Text = "Add to Existing Folder", AutoSize = true };
                addBtn.Click += (s, e) => { choice = "existing"; dlg.Close(); };
                btnPanel.Controls.Add(addBtn);

                var newBtn = new Button { Text = "Create New Folder", AutoSize = true };
                newBtn.Click += (s, e) => { choice = "new"; dlg.Close(); };
                btnPanel.Controls.Add(newBtn);

                var cancelBtn = new Button { Text = "Cancel", AutoSize = true };
                cancelBtn.Click += (s, e) => { choice = "cancel"; dlg.Close(); };
                btnPanel.Controls.Add(cancelBtn);

                dlg.Controls.Add(btnPanel);
                dlg.ShowDialog(this);

                if (choice == "existing" && existingFolders.Count > 1)
                {
                    // Let user choose which folder
                    return ChooseExistingFolder(existingFolders);
                }
                else if (choice == "existing")
                {
                    return existingFolders[0]["path"];
                }
                else if (choice == "new")
                {
                    return null; // Signal to create new
                }
                return ""; // Cancel
            }
        }

        string ChooseExistingFolder(List<Dictionary<string, string>> existingFolders)
        {
            using (var dlg = new Form
            {
                Text = "Choose Existing Folder",
                Size = new Size(600, 400),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false
            })
            {
                var listBox = new ListBox { Dock = DockStyle.Fill };
                foreach (var f in existingFolders)
                    listBox.Items.Add(f["name"]);
                listBox.SelectedIndex = 0;
                dlg.Controls.Add(listBox);

                var btnPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 40,
                    FlowDirection = FlowDirection.RightToLeft
                };
                string result = "";
                var okBtn = new Button { Text = "OK", DialogResult = DialogResult.OK };
                okBtn.Click += (s, e) =>
                {
                    if (listBox.SelectedIndex >= 0)
                        result = existingFolders[listBox.SelectedIndex]["path"];
                };
                btnPanel.Controls.Add(okBtn);
                var cancelBtn = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
                btnPanel.Controls.Add(cancelBtn);
                dlg.Controls.Add(btnPanel);
                dlg.AcceptButton = okBtn;
                dlg.CancelButton = cancelBtn;

                return dlg.ShowDialog(this) == DialogResult.OK ? result : "";
            }
        }

        void SaveFile()
        {
            string make = _makeInput.Text.Trim();
            string model = _modelInput.Text.Trim().Replace(' ', '_');
            string date = _dateInput.Text.Trim();
            string ecu = _ecuInput.Text.Trim().Replace(' ', '_');
            string swVer = _swVersionInput.Text.Trim();
            string readMethod = _readMethodCombo.SelectedItem?.ToString() ?? "";
            string mileage = _mileageInput.Text.Trim();
            string registration = _registrationInput.Text.Trim().Replace(' ', '_');

            string boschSw = _boschSwInput.Text.Trim();
            string oemHw = _oemHwInput.Text.Trim();
            string oemSw = _oemSwInput.Text.Trim();
            string engineCode = _engineCodeInput.Text.Trim();
            string engineType = _engineTypeInput.Text.Trim();

            if (make == "" || model == "")
            {
                MessageBox.Show(this, "Please fill in at least Make and Model!",
                    "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Check for existing folders with same registration
            string destFolder = null;

            if (registration != "")
            {
                var existing = CheckExistingFolders(registration);
                if (existing.Count > 0)
                {
                    string choice = ShowDuplicateDialog(existing);
                    if (choice == "") return; // Cancel
                    if (choice != null) destFolder = choice; // Use existing folder
                }
            }

            // If not using existing folder, create new one
            if (destFolder == null)
            {
                string rmShort = readMethod.Replace("Normal Read-", "").Replace("Virtual Read-", "Virtual");
                string folderName = $"{make}_{model}_{date}_{ecu}";
                if (swVer != "") folderName += $"_SW{swVer}";
                folderName += $"_{rmShort}";
                if (mileage != "") folderName += $"_{mileage}km";
                if (registration != "") folderName += $"_{registration}";
                destFolder = Path.Combine(_destinationBase, make, folderName);
            }

            try
            {
                Directory.CreateDirectory(destFolder);

                string filename = Path.GetFileName(_filePath);
                string destPath = Path.Combine(destFolder, filename);

                // Check if file already exists
                if (File.Exists(destPath))
                {
                    string baseName = Path.GetFileNameWithoutExtension(filename);
                    string ext = Path.GetExtension(filename);
                    string timestamp = DateTime.Now.ToString("HHmmss");
                    filename = $"{baseName}_{timestamp}{ext}";
                    destPath = Path.Combine(destFolder, filename);
                }

                File.Move(_filePath, destPath);

                // Create Log.txt
                CreateLogFile(destFolder, make, model, date, ecu, readMethod,
                    mileage, registration, filename, destPath,
                    swVer, boschSw, oemHw, oemSw, engineCode, engineType);

                // Open folder if setting enabled
                if (_parentWindow?.Settings?.OpenFolderOnSave == true)
                {
                    try { Process.Start("explorer.exe", destFolder); } catch { }
                }

                FileSaved?.Invoke(destPath);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to save file:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void CreateLogFile(string destFolder, string make, string model, string date,
            string ecu, string readMethod, string mileage, string registration,
            string filename, string destPath, string swVersion, string boschSw,
            string oemHw, string oemSw, string engineCode, string engineType)
        {
            try
            {
                string logPath = Path.Combine(destFolder, "Log.txt");
                long fileSize = new FileInfo(destPath).Length;
                double fileSizeMb = fileSize / (1024.0 * 1024.0);
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                bool isNew = !File.Exists(logPath);

                string entry = "";
                if (isNew)
                {
                    entry = $@"ECU File Organization Log
{new string('=', 70)}

Vehicle: {make} {model} | Registration: {registration}
Folder: {Path.GetFileName(destFolder)}

{new string('=', 70)}

";
                }
                else
                {
                    entry = $"\n{new string('=', 70)}\n\n";
                }

                entry += $@"SESSION {timestamp}
{new string('-', 70)}

File: {filename}
Size: {fileSizeMb:F2} MB ({fileSize:N0} bytes)

Vehicle Information:
  Make:           {make}
  Model:          {model}
  Date:           {date}
  ECU Type:       {ecu}
  Read Method:    {readMethod}
  Mileage:        {mileage} km
  Registration:   {registration}

ECU Metadata (from BIN):
  Software Version: {swVersion}
  Bosch SW Number:  {boschSw}
  OEM HW Number:    {oemHw}
  OEM SW Number:    {oemSw}
  Engine Code:      {engineCode}
  Engine Type:      {engineType}

Full Path: {destPath}

Organized by ECU File Organizer v{Constants.AppVersion}
{Constants.SupportUrl}
";

                File.AppendAllText(logPath, entry, System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }
}
