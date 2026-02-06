# 🔧 ECU File Organizer

Automatic ECU file organizer for automotive diagnostics. Monitors a folder for new ECU `.bin` files from Autotuner, parses the filename, and organizes files into a structured directory with a pop-up form.

## ✨ Features

- **🔍 Automatic Monitoring**: Watches specified folder for new `.bin` files
- **📋 Pop-up Form**: Automatically appears when new file is detected with 7 fields
- **🤖 Smart Parsing**: Extracts Make, Model, ECU type, and Read Method from filename
- **🔄 Read Method Dropdown**: Select OBD, Virtual OBD, Bench, or Boot (auto-detected)
- **📁 Auto-Organization**: Creates structured folders with read method tracking
- **🚀 File Moving**: Moves files (not copy) - no duplicates, cleaner workflow
- **💾 System Tray**: Runs in background with tray icon
- **🚀 Windows Startup**: Checkbox to enable/disable auto-start with Windows
- **❌ Exit Button**: Dedicated button to fully quit the application
- **⚡ Real-time**: Detects files as they're saved
- **🔧 Works with ANY tool**: Not just Autotuner - KESS, CMD, Alientech, etc.

## 📋 Requirements

- Windows 10/11
- Python 3.8 or higher
- PyQt6

**Note**: The application uses **built-in file monitoring** (no watchdog library needed). Only PyQt6 is required!

## 🚀 Installation

### Method 1: Using pip

1. **Extract the files** to a folder (e.g., `C:\ECU_Organizer`)

2. **Open Command Prompt** in that folder:
   ```
   cd C:\ECU_Organizer
   ```

3. **Install dependencies**:
   ```
   pip install -r requirements.txt
   ```

4. **Run the application**:
   ```
   pythonw ecu_file_organizer.py
   ```

### Method 2: Create Executable (Recommended)

1. **Install PyInstaller**:
   ```
   pip install pyinstaller
   ```

2. **Create executable**:
   ```
   pyinstaller --onefile --windowed --name "ECU_Organizer" --icon=icon.ico ecu_file_organizer.py
   ```

3. **Find executable** in `dist/ECU_Organizer.exe`

4. **Run** `ECU_Organizer.exe` - no Python needed!

## 🎯 Usage

### First Time Setup

1. **Launch the application**
   - You'll see the main window with settings

2. **Configure folders**:
   - **Monitor Folder**: Where Autotuner saves files (default: `Desktop/AutotunerFiles`)
   - **Destination Folder**: Where organized files go (default: `Desktop/ECU_files`)

3. **Click "Start Monitoring"**
   - Application starts watching for new files
   - Window can be minimized to tray

### When New File is Detected

1. **Pop-up appears automatically** with pre-filled information:
   - **Make**: Volkswagen *(from filename)*
   - **Model**: Golf 2008 VI *(from filename)*
   - **Date**: 20250123 *(today's date)*
   - **ECU**: Siemens PCR2.1 *(from filename)*
   - **Read Method**: Normal Read-OBD *(dropdown, auto-detected)*
   - **Mileage**: *[empty - you fill this in]*
   - **Registration**: *[empty - you fill this in]*

2. **Edit if needed**:
   - Fix any incorrect parsing
   - Select correct read method from dropdown
   - Add mileage (most important)
   - Add registration number
   - Press `Tab` to move between fields

3. **Click "Save & Organize"** or press `Enter`
   - File is **moved** to organized location (not copied)
   - Original file removed from monitored folder
   - Notification shows success

**Important**: Files are **moved**, not copied. After organizing, the original file will be removed from your monitored folder and placed in the organized location.

### File Organization Structure

```
Desktop/
└── ECU_files/
    ├── Volkswagen/
    │   ├── Volkswagen_Golf_2008_VI_20250123_PCR2.1_OBD_45000km_AB12345/
    │   │   └── Volkswagen_Golf_2008__VI__1_6_TDI_CR_105_hp_Siemens_PCR2_1_OBD_NR.bin
    │   └── Volkswagen_Passat_2015_B8_20250123_Bosch_EDC17_Bench_78000km_CD67890/
    │       └── [original file].bin
    ├── Opel/
    │   └── Opel_Astra_2019_K_20250123_Marelli_OBD_125000km_EF11223/
    │       └── [original file].bin
    └── Peugeot/
        └── Peugeot_308_2020_T9_20250123_Bosch_MED17_Boot_32000km_GH44556/
            └── [original file].bin
```

## 🔄 Autotuner Filename Format

The application recognizes Autotuner's naming convention:

**Example**: `Volkswagen_Golf_2008__VI__1_6_TDI_CR_105_hp_Siemens_PCR2_1_OBD_NR.bin`

**Parsed as**:
- Make: `Volkswagen`
- Model: `Golf 2008 VI`
- Engine: `1.6 TDI CR 105 hp` *(info only)*
- ECU: `Siemens PCR2.1`
- Method: `OBD` *(info only)*
- Status: `NR` (New Read) *(info only)*

## 🛠️ Using Files from Other Tools

**The app works with ANY .bin file**, not just Autotuner!

**If filename is not in Autotuner format**:
- Pop-up will still appear
- Fields might be empty or partially filled
- **Just fill them in manually** - it works perfectly!
- You can organize files from KESS, CMD, Alientech, or any other tool

**Examples**:
- `my_file_123.bin` → All fields empty, you fill manually
- `Golf_GTI_Stage1.bin` → Some fields detected, you complete the rest
- `customer_car_ecu_read.bin` → Fill all fields manually

The parsing is just a **convenience feature** - manual entry always works!

## 🚀 Windows Startup Configuration

### Add to Startup (Auto-start with Windows)

**Method 1: Using Script**
```
python startup_config.py
```
- Choose option `1` to add to startup

**Method 2: Manual**
1. Press `Win + R`
2. Type: `shell:startup`
3. Create shortcut to `ECU_Organizer.exe` in opened folder

### Remove from Startup

**Method 1: Using Script**
```
python startup_config.py
```
- Choose option `2` to remove from startup

**Method 2: Manual**
1. Press `Ctrl + Shift + Esc` (Task Manager)
2. Go to "Startup" tab
3. Find "ECU File Organizer" and disable

## 💡 Tips & Tricks

### Quick Workflow
1. Read ECU with Autotuner → saves to monitored folder
2. Pop-up appears automatically
3. Add mileage, press `Enter`
4. Done! File is organized

### Keyboard Shortcuts
- `Tab`: Move between fields
- `Enter`: Save & Organize
- `Esc`: Cancel

### System Tray
- **Double-click icon**: Show main window
- **Right-click icon**: 
  - Show Window
  - Quit

### Best Practices
1. **Always add mileage** - crucial for future reference
2. **Check date format** - use YYYYMMDD (e.g., 20250123)
3. **Review parsed data** - Autotuner format varies slightly
4. **Keep original files** - application copies, doesn't move

## 🐛 Troubleshooting

### Pop-up doesn't appear
- ✅ Check monitoring is active (green status)
- ✅ Verify monitor folder path is correct
- ✅ Ensure file is fully written (wait 2-3 seconds after save)

### File not parsed correctly
- ✅ Check filename follows Autotuner format
- ✅ Manually edit fields in pop-up form
- ✅ Original file remains unchanged

### Application not starting with Windows
- ✅ Run `startup_config.py` and check status
- ✅ Check Task Manager > Startup tab
- ✅ Ensure executable path is correct

### Can't find organized files
- ✅ Check destination folder setting
- ✅ Look in subfolder by Make (e.g., `ECU_files/Volkswagen/`)
- ✅ Check preview path in pop-up form

## 📁 Files Included

```
ECU_Organizer/
├── ecu_file_organizer.py     # Main application
├── startup_config.py          # Startup configuration tool
├── requirements.txt           # Python dependencies
├── README.md                  # This file
└── icon.ico                   # Application icon (optional)
```

## 🔧 Configuration

Settings are saved automatically in:
```
C:\Users\[YourName]\.ecu_organizer_settings.json
```

You can manually edit this file to change:
- `monitor_folder`: Folder to watch
- `destination_base`: Where to organize files
- `run_on_startup`: Auto-start with Windows

## 📝 Example Workflow

**Scenario**: You just read a 2020 Peugeot 308 ECU with 32,450 km, registration: AB-12345

1. **Autotuner saves file**:
   ```
   Peugeot_308_2020__T9__1_5_BlueHDI_130_hp_Bosch_MED17_OBD_NR.bin
   ```

2. **Pop-up appears** with:
   - Make: `Peugeot` ✅
   - Model: `308 2020 T9` ✅
   - Date: `20250123` ✅
   - ECU: `Bosch MED17` ✅
   - Read Method: `Normal Read-OBD` ✅ (dropdown, auto-detected)
   - Mileage: `[empty]` ⬅️ **You add: 32450**
   - Registration: `[empty]` ⬅️ **You add: AB12345**

3. **Press Enter**, file is **moved** to:
   ```
   Desktop/ECU_files/Peugeot/Peugeot_308_2020_T9_20250123_Bosch_MED17_OBD_32450km_AB12345/
   ```
   Original file is removed from monitored folder.

4. **Notification confirms** success!

## 🎨 Customization

### Change Folder Structure
Edit the `save_file()` function in `ecu_file_organizer.py`:
```python
# Current structure
folder_name = f"{make}_{model}_{date}_{ecu}_{mileage}km"

# Alternative: No date
folder_name = f"{make}_{model}_{ecu}_{mileage}km"

# Alternative: Customer name
folder_name = f"{customer_name}_{make}_{model}_{mileage}km"
```

### Add More Fields
Add to `ECUFormDialog` class:
```python
# Add customer name field
self.customer_input = QLineEdit()
form_layout.addRow("Customer:", self.customer_input)
```

## 🆘 Support

For issues or questions:
1. Check this README thoroughly
2. Verify all requirements are installed
3. Test with sample file from Autotuner
4. Check Windows Event Viewer for Python errors

## 📜 Version History

**v1.0** (January 2025)
- Initial release
- Autotuner filename parsing
- Pop-up form with pre-filled data
- System tray integration
- Windows startup support
- Automatic folder organization

## 🙏 Credits

Developed for automotive diagnostic professionals working with ECU files.

---

**Made with ❤️ for automotive proffesionals**

Organize your ECU files effortlessly! 🚗💾
