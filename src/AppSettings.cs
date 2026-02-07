using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace ECUFileOrganizer
{
    /// <summary>Application settings management with JSON persistence.</summary>
    class AppSettings
    {
        static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ecu_organizer_settings.json");

        static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public string MonitorFolder { get; set; }
        public string DestinationBase { get; set; }
        public bool RunOnStartup { get; set; }
        public bool OpenFolderOnSave { get; set; }
        public List<Dictionary<string, object>> RecentFiles { get; set; }
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }

        public AppSettings()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            MonitorFolder = Path.Combine(desktop, "AutotunerFiles");
            DestinationBase = Path.Combine(desktop, "ECU_files");
            RunOnStartup = true;
            OpenFolderOnSave = true;
            RecentFiles = new List<Dictionary<string, object>>();
            WindowX = -1;
            WindowY = -1;
            WindowWidth = 750;
            WindowHeight = 620;
        }

        public void Load()
        {
            if (!File.Exists(SettingsPath)) return;

            try
            {
                string json = File.ReadAllText(SettingsPath);
                var dict = Json.Deserialize<Dictionary<string, object>>(json);
                if (dict == null) return;

                if (dict.ContainsKey("monitor_folder"))
                    MonitorFolder = dict["monitor_folder"]?.ToString() ?? MonitorFolder;
                if (dict.ContainsKey("destination_base"))
                    DestinationBase = dict["destination_base"]?.ToString() ?? DestinationBase;
                if (dict.ContainsKey("run_on_startup"))
                    RunOnStartup = Convert.ToBoolean(dict["run_on_startup"]);
                if (dict.ContainsKey("open_folder_on_save"))
                    OpenFolderOnSave = Convert.ToBoolean(dict["open_folder_on_save"]);

                if (dict.ContainsKey("window_x"))
                    WindowX = Convert.ToInt32(dict["window_x"]);
                if (dict.ContainsKey("window_y"))
                    WindowY = Convert.ToInt32(dict["window_y"]);
                if (dict.ContainsKey("window_width"))
                    WindowWidth = Convert.ToInt32(dict["window_width"]);
                if (dict.ContainsKey("window_height"))
                    WindowHeight = Convert.ToInt32(dict["window_height"]);

                if (dict.ContainsKey("recent_files") && dict["recent_files"] is ArrayList arr)
                {
                    RecentFiles.Clear();
                    foreach (var item in arr)
                    {
                        if (item is Dictionary<string, object> entry)
                            RecentFiles.Add(entry);
                    }
                }
            }
            catch
            {
                // Use defaults on any error
            }
        }

        public void Save()
        {
            try
            {
                var dict = new Dictionary<string, object>
                {
                    ["monitor_folder"] = MonitorFolder,
                    ["destination_base"] = DestinationBase,
                    ["run_on_startup"] = RunOnStartup,
                    ["open_folder_on_save"] = OpenFolderOnSave,
                    ["recent_files"] = RecentFiles,
                    ["window_x"] = WindowX,
                    ["window_y"] = WindowY,
                    ["window_width"] = WindowWidth,
                    ["window_height"] = WindowHeight
                };

                string json = Json.Serialize(dict);
                // Simple pretty-print
                json = json.Replace(",\"", ",\n    \"").Replace("{\"", "{\n    \"").Replace("}", "\n}");
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Silently fail
            }
        }

        public void AddRecentFile(string destPath)
        {
            try
            {
                string folderPath = Path.GetDirectoryName(destPath);
                string folderName = Path.GetFileName(folderPath);
                string filename = Path.GetFileName(destPath);
                string parentFolder = Path.GetFileName(Path.GetDirectoryName(folderPath));

                var entry = new Dictionary<string, object>
                {
                    ["folder_path"] = folderPath,
                    ["folder_name"] = folderName,
                    ["filename"] = filename,
                    ["make"] = parentFolder,
                    ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                RecentFiles.Insert(0, entry);
                if (RecentFiles.Count > 20)
                    RecentFiles.RemoveRange(20, RecentFiles.Count - 20);

                Save();
            }
            catch { }
        }
    }
}
