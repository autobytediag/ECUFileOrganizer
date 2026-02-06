"""File monitoring thread for watching a folder for new .bin files."""

import os

from PyQt6.QtCore import QThread, pyqtSignal


class FileMonitor(QThread):
    """Background thread for monitoring folder"""
    file_detected = pyqtSignal(str)

    def __init__(self, monitor_folder):
        super().__init__()
        self.monitor_folder = monitor_folder
        self.running = True
        self.processed_files = set()

    def run(self):
        """Monitor folder for new .bin files"""
        while self.running:
            if os.path.exists(self.monitor_folder):
                try:
                    files = [f for f in os.listdir(self.monitor_folder)
                            if f.lower().endswith('.bin')]

                    for file in files:
                        file_path = os.path.join(self.monitor_folder, file)
                        if file_path not in self.processed_files:
                            # Check if file is fully written (size stable)
                            try:
                                size1 = os.path.getsize(file_path)
                                self.msleep(500)
                                size2 = os.path.getsize(file_path)

                                if size1 == size2:
                                    self.processed_files.add(file_path)
                                    self.file_detected.emit(file_path)
                            except:
                                pass
                except:
                    pass

            self.msleep(2000)  # Check every 2 seconds

    def stop(self):
        self.running = False
