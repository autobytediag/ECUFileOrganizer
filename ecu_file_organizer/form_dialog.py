"""Pop-up form dialog for ECU file information entry."""

import os
import shutil
from datetime import datetime

from PyQt6.QtWidgets import (QWidget, QVBoxLayout, QHBoxLayout, QLabel, QLineEdit,
                              QPushButton, QMessageBox, QGroupBox, QFormLayout,
                              QComboBox, QDialog, QListWidget, QDialogButtonBox,
                              QApplication)
from PyQt6.QtCore import Qt, pyqtSignal
from PyQt6.QtGui import QFont

from ecu_file_organizer.constants import READ_METHODS, APP_VERSION, SUPPORT_URL


class ECUFormDialog(QWidget):
    """Pop-up form for ECU file information"""
    file_saved = pyqtSignal(str)

    def __init__(self, file_path, parsed_data, destination_base, parent_window=None):
        super().__init__()
        self.file_path = file_path
        self.destination_base = destination_base
        self.parent_window = parent_window
        self.init_ui(parsed_data)

    def init_ui(self, parsed_data):
        """Initialize the form UI"""
        self.setWindowTitle("New ECU File Detected")
        self.setWindowFlags(Qt.WindowType.WindowStaysOnTopHint | Qt.WindowType.Dialog)
        self.setMinimumWidth(500)

        layout = QVBoxLayout()

        # Title
        title = QLabel("New ECU File Detected")
        title_font = QFont()
        title_font.setPointSize(12)
        title_font.setBold(True)
        title.setFont(title_font)
        title.setAlignment(Qt.AlignmentFlag.AlignCenter)
        layout.addWidget(title)

        # File info
        file_label = QLabel(f"File: {os.path.basename(self.file_path)}")
        file_label.setStyleSheet("color: #666; padding: 5px;")
        layout.addWidget(file_label)

        # Form group
        form_group = QGroupBox("ECU Information")
        form_layout = QFormLayout()

        # Make
        self.make_input = QLineEdit()
        self.make_input.setText(parsed_data['make'])
        self.make_input.textChanged.connect(self.update_preview)
        form_layout.addRow("Make:", self.make_input)

        # Model
        self.model_input = QLineEdit()
        self.model_input.setText(parsed_data['model'])
        self.model_input.textChanged.connect(self.update_preview)
        form_layout.addRow("Model:", self.model_input)

        # Date
        self.date_input = QLineEdit()
        self.date_input.setText(parsed_data['date'])
        self.date_input.setPlaceholderText("YYYYMMDD")
        self.date_input.textChanged.connect(self.update_preview)
        form_layout.addRow("Date:", self.date_input)

        # ECU
        self.ecu_input = QLineEdit()
        self.ecu_input.setText(parsed_data['ecu'])
        self.ecu_input.textChanged.connect(self.update_preview)
        form_layout.addRow("ECU Type:", self.ecu_input)

        # Read Method dropdown
        self.read_method_combo = QComboBox()
        self.read_method_combo.addItems(READ_METHODS)
        # Set from parsed data if available
        if parsed_data['read_method']:
            index = self.read_method_combo.findText(parsed_data['read_method'])
            if index >= 0:
                self.read_method_combo.setCurrentIndex(index)
        self.read_method_combo.currentTextChanged.connect(self.update_preview)
        form_layout.addRow("Read Method:", self.read_method_combo)

        # Mileage
        self.mileage_input = QLineEdit()
        self.mileage_input.setText(parsed_data['mileage'])
        self.mileage_input.setPlaceholderText("e.g., 45000")
        self.mileage_input.textChanged.connect(self.update_preview)
        form_layout.addRow("Mileage (km):", self.mileage_input)

        # Registration number
        self.registration_input = QLineEdit()
        self.registration_input.setText(parsed_data['registration'])
        self.registration_input.setPlaceholderText("e.g., AB12345")
        self.registration_input.textChanged.connect(self.update_preview)
        form_layout.addRow("Registration No:", self.registration_input)

        form_group.setLayout(form_layout)
        layout.addWidget(form_group)

        # Preview
        preview_group = QGroupBox("Destination Preview")
        preview_layout = QVBoxLayout()
        self.preview_label = QLabel()
        self.preview_label.setWordWrap(True)
        self.preview_label.setStyleSheet("background-color: #f0f0f0; padding: 10px; border-radius: 5px;")
        preview_layout.addWidget(self.preview_label)
        preview_group.setLayout(preview_layout)
        layout.addWidget(preview_group)

        # Buttons
        button_layout = QHBoxLayout()

        cancel_btn = QPushButton("Cancel")
        cancel_btn.clicked.connect(self.reject)
        button_layout.addWidget(cancel_btn)

        button_layout.addStretch()

        save_btn = QPushButton("Save && Organize")
        save_btn.setStyleSheet("""
            QPushButton {
                background-color: #4CAF50;
                color: white;
                padding: 8px 16px;
                font-weight: bold;
                border-radius: 4px;
            }
            QPushButton:hover {
                background-color: #45a049;
            }
        """)
        save_btn.clicked.connect(self.save_file)
        button_layout.addWidget(save_btn)

        layout.addLayout(button_layout)

        self.setLayout(layout)

        # Set focus to mileage input (first empty field)
        self.mileage_input.setFocus()

        # Update preview
        self.update_preview()

        # Center on screen
        self.center_on_screen()

    def center_on_screen(self):
        """Center the window on screen"""
        screen = QApplication.primaryScreen().geometry()
        size = self.geometry()
        self.move(
            (screen.width() - size.width()) // 2,
            (screen.height() - size.height()) // 2
        )

    def update_preview(self):
        """Update destination path preview"""
        make = self.make_input.text().strip()
        model = self.model_input.text().strip().replace(' ', '_')
        date = self.date_input.text().strip()
        ecu = self.ecu_input.text().strip().replace(' ', '_')
        read_method = self.read_method_combo.currentText()
        mileage = self.mileage_input.text().strip()
        registration = self.registration_input.text().strip().replace(' ', '_')

        # Shorten read method for folder name
        read_method_short = read_method.replace('Normal Read-', '').replace('Virtual Read-', 'Virtual')

        if make and model:
            folder_name = f"{make}_{model}_{date}_{ecu}_{read_method_short}"
            if mileage:
                folder_name += f"_{mileage}km"
            if registration:
                folder_name += f"_{registration}"

            dest_path = os.path.join(self.destination_base, make, folder_name)
            self.preview_label.setText(dest_path)
        else:
            self.preview_label.setText("Please fill in Make and Model")

    def check_existing_folders(self, registration):
        """Check if folders exist for this registration number"""
        existing_folders = []

        if not registration or not os.path.exists(self.destination_base):
            return existing_folders

        # Clean registration for matching
        registration_clean = registration.strip().replace(' ', '_')

        # Search through all make folders
        try:
            for make_name in os.listdir(self.destination_base):
                make_folder = os.path.join(self.destination_base, make_name)
                if not os.path.isdir(make_folder):
                    continue

                # Look through all folders in this make
                for folder_name in os.listdir(make_folder):
                    folder_path = os.path.join(make_folder, folder_name)
                    if os.path.isdir(folder_path):
                        # Check if folder name ends with this registration number
                        if folder_name.endswith(f"_{registration_clean}"):
                            existing_folders.append({
                                'path': folder_path,
                                'name': folder_name
                            })
        except Exception as e:
            print(f"Error checking existing folders: {e}")

        # Sort by name (newest first usually)
        existing_folders.sort(key=lambda x: x['name'], reverse=True)

        return existing_folders

    def show_duplicate_dialog(self, existing_folders):
        """Show dialog to choose between existing folder or creating new one"""
        msg = QMessageBox(self)
        msg.setWindowTitle("Duplicate Detected")
        msg.setIcon(QMessageBox.Icon.Question)

        # Extract registration from first folder name
        first_folder = existing_folders[0]['name']
        registration = first_folder.split('_')[-1] if '_' in first_folder else "this registration"

        # Build message
        text = f"<h3>Found {len(existing_folders)} existing folder(s) for registration: <b>{registration}</b></h3>"
        text += "<p><b>Existing folders:</b></p><ul>"
        for folder in existing_folders[:5]:  # Show max 5
            text += f"<li><small>{folder['name']}</small></li>"
        if len(existing_folders) > 5:
            text += f"<li><i>... and {len(existing_folders) - 5} more</i></li>"
        text += "</ul>"
        text += "<p><b>What do you want to do?</b></p>"
        text += "<p><i>Add to existing: Keep all files together<br>"
        text += "Create new: Separate by date/service</i></p>"

        msg.setText(text)
        msg.setTextFormat(Qt.TextFormat.RichText)

        # Add custom buttons
        add_existing_btn = msg.addButton("Add to Existing Folder", QMessageBox.ButtonRole.ActionRole)
        create_new_btn = msg.addButton("Create New Folder", QMessageBox.ButtonRole.ActionRole)
        cancel_btn = msg.addButton("Cancel", QMessageBox.ButtonRole.RejectRole)

        msg.exec()

        clicked_button = msg.clickedButton()

        if clicked_button == add_existing_btn:
            # If multiple folders, let user choose which one
            if len(existing_folders) > 1:
                return self.choose_existing_folder(existing_folders)
            else:
                return ('existing', existing_folders[0]['path'])
        elif clicked_button == create_new_btn:
            return ('new', None)
        else:
            return ('cancel', None)

    def choose_existing_folder(self, existing_folders):
        """Let user choose which existing folder to use"""
        dialog = QDialog(self)
        dialog.setWindowTitle("Choose Existing Folder")
        dialog.setMinimumWidth(600)
        dialog.setMinimumHeight(400)

        layout = QVBoxLayout()

        label = QLabel("<h3>Select folder to add file to:</h3>")
        label.setTextFormat(Qt.TextFormat.RichText)
        layout.addWidget(label)

        list_widget = QListWidget()
        for folder in existing_folders:
            list_widget.addItem(folder['name'])
        list_widget.setCurrentRow(0)  # Select first by default
        layout.addWidget(list_widget)

        button_box = QDialogButtonBox(
            QDialogButtonBox.StandardButton.Ok | QDialogButtonBox.StandardButton.Cancel
        )
        button_box.accepted.connect(dialog.accept)
        button_box.rejected.connect(dialog.reject)
        layout.addWidget(button_box)

        dialog.setLayout(layout)

        if dialog.exec() == QDialog.DialogCode.Accepted:
            selected_index = list_widget.currentRow()
            if selected_index >= 0:
                return ('existing', existing_folders[selected_index]['path'])

        return ('cancel', None)

    def save_file(self):
        """Save and organize the file"""
        make = self.make_input.text().strip()
        model = self.model_input.text().strip().replace(' ', '_')
        date = self.date_input.text().strip()
        ecu = self.ecu_input.text().strip().replace(' ', '_')
        read_method = self.read_method_combo.currentText()
        mileage = self.mileage_input.text().strip()
        registration = self.registration_input.text().strip().replace(' ', '_')

        # Validation
        if not make or not model:
            QMessageBox.warning(self, "Missing Information",
                              "Please fill in at least Make and Model!")
            return

        # Check for existing folders with same registration number
        existing_folders = []
        if registration:  # Only check if registration is provided
            existing_folders = self.check_existing_folders(registration)

        dest_folder = None

        if existing_folders:
            # Show duplicate dialog
            choice, folder_path = self.show_duplicate_dialog(existing_folders)

            if choice == 'cancel':
                return  # User cancelled
            elif choice == 'existing':
                # Use existing folder
                dest_folder = folder_path
            elif choice == 'new':
                # Create new folder (continue with normal flow)
                pass

        # If not using existing folder, create new one
        if dest_folder is None:
            # Shorten read method for folder name
            read_method_short = read_method.replace('Normal Read-', '').replace('Virtual Read-', 'Virtual')

            # Build destination path
            folder_name = f"{make}_{model}_{date}_{ecu}_{read_method_short}"
            if mileage:
                folder_name += f"_{mileage}km"
            if registration:
                folder_name += f"_{registration}"

            dest_folder = os.path.join(self.destination_base, make, folder_name)

        try:
            # Create destination folder (if it doesn't exist)
            os.makedirs(dest_folder, exist_ok=True)

            # Move file (not copy)
            filename = os.path.basename(self.file_path)
            dest_path = os.path.join(dest_folder, filename)

            # Check if file already exists in destination
            if os.path.exists(dest_path):
                # Add timestamp to avoid overwriting
                base_name, ext = os.path.splitext(filename)
                timestamp = datetime.now().strftime("%H%M%S")
                filename = f"{base_name}_{timestamp}{ext}"
                dest_path = os.path.join(dest_folder, filename)

            shutil.move(self.file_path, dest_path)

            # Create Log.txt file
            self.create_log_file(dest_folder, make, model, date, ecu, read_method,
                               mileage, registration, filename, dest_path)

            # Open folder if setting is enabled
            if self.parent_window and self.parent_window.settings.get('open_folder_on_save', False):
                try:
                    # Open folder in Windows Explorer
                    os.startfile(dest_folder)
                except Exception as e:
                    print(f"Could not open folder: {e}")

            # Emit signal and close
            self.file_saved.emit(dest_path)
            self.close()

        except Exception as e:
            QMessageBox.critical(self, "Error", f"Failed to save file:\n{str(e)}")

    def create_log_file(self, dest_folder, make, model, date, ecu, read_method,
                       mileage, registration, filename, dest_path):
        """Create or append to Log.txt file with session information"""
        try:
            log_path = os.path.join(dest_folder, "Log.txt")

            # Get file size
            file_size = os.path.getsize(dest_path)
            file_size_mb = file_size / (1024 * 1024)

            # Get current timestamp
            timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")

            # Check if log file already exists
            is_new_log = not os.path.exists(log_path)

            # Build log entry
            if is_new_log:
                # First entry - create header
                log_entry = f"""ECU File Organization Log
{'=' * 70}

Vehicle: {make} {model} | Registration: {registration}
Folder: {os.path.basename(dest_folder)}

{'=' * 70}

"""
            else:
                # Subsequent entry - add separator
                log_entry = f"\n{'=' * 70}\n\n"

            # Add session entry
            log_entry += f"""SESSION {timestamp}
{'-' * 70}

File: {filename}
Size: {file_size_mb:.2f} MB ({file_size:,} bytes)

Vehicle Information:
  Make:           {make}
  Model:          {model}
  Date:           {date}
  ECU Type:       {ecu}
  Read Method:    {read_method}
  Mileage:        {mileage} km
  Registration:   {registration}

Full Path: {dest_path}

Organized by ECU File Organizer v{APP_VERSION}
{SUPPORT_URL}
"""

            # Append to log file (or create if new)
            mode = 'w' if is_new_log else 'a'
            with open(log_path, mode, encoding='utf-8') as f:
                f.write(log_entry)

            session_text = "first session" if is_new_log else "new session"
            print(f"Log file updated: {log_path} ({session_text})")

        except Exception as e:
            print(f"Error creating/updating log file: {e}")
            # Don't show error to user - log creation is not critical

    def reject(self):
        """Cancel and close"""
        self.close()
