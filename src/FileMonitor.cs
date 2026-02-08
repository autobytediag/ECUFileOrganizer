using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace ECUFileOrganizer
{
    /// <summary>Background thread for monitoring a folder for new .bin files.</summary>
    class FileMonitor
    {
        readonly string _monitorFolder;
        volatile bool _running;
        Thread _thread;
        readonly HashSet<string> _processedFiles = new HashSet<string>();

        public event Action<string> FileDetected;

        public FileMonitor(string monitorFolder)
        {
            _monitorFolder = monitorFolder;
        }

        public void Start()
        {
            _running = true;
            _thread = new Thread(Run) { IsBackground = true, Name = "FileMonitor" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            _thread?.Join(3000);
        }

        public bool IsRunning => _running && (_thread?.IsAlive ?? false);

        void Run()
        {
            while (_running)
            {
                if (Directory.Exists(_monitorFolder))
                {
                    try
                    {
                        foreach (string file in Directory.GetFiles(_monitorFolder, "*.bin"))
                        {
                            if (!_running) break;
                            if (_processedFiles.Contains(file)) continue;

                            try
                            {
                                // Check if file is fully written (size stable)
                                long size1 = new FileInfo(file).Length;
                                Thread.Sleep(500);
                                long size2 = new FileInfo(file).Length;

                                if (size1 == size2)
                                {
                                    _processedFiles.Add(file);
                                    FileDetected?.Invoke(file);
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                Thread.Sleep(2000); // Check every 2 seconds
            }
        }
    }
}
