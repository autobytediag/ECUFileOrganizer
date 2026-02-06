"""Shared constants for ECU File Organizer."""

APP_VERSION = "1.11"
APP_NAME = "ECU File Organizer"
APP_DISPLAY_NAME = f"ECU File Organizer v{APP_VERSION}"
SUPPORT_URL = "https://buymeacoffee.com/autobyte"

ECU_BRANDS = [
    'Bosch', 'Siemens', 'Delphi', 'Continental',
    'Marelli', 'Valeo', 'Denso', 'PCR', 'EDC', 'MED',
]

READ_METHODS = [
    "Normal Read-OBD",
    "Virtual Read-OBD",
    "Bench",
    "Boot",
]

# Regex patterns for Bosch BIN file metadata extraction
# Bosch SW number (10 digits, starts with 103x/109x)
BOSCH_SW_PATTERN = rb'(10[3-9]\d{7})'

# VAG OEM part numbers (e.g. 03L907309AE) with boundary assertions
OEM_NUMBER_PATTERN = rb'(?<![0-9A-Za-z])(\d{2,3}[A-Z]\d{6}[A-Z]{0,3})(?![0-9A-Za-z])'

# ECU type identifiers - explicit alternatives for known Bosch ECU formats
ECU_TYPE_PATTERN = rb'(MDG1_MD1CS\d{3}|MD1CS\d{3}|EDC\d+_[A-Z]?\d+|MED\d+_[A-Z]?\d+|MEVD\d+_[A-Z]?\d+|MG1_[A-Z]{1,2}\d+)'

# Bosch firmware version path (e.g. "38/1/MDG1_MD1CS001/11/P_1401//VLWT0///")
BOSCH_PATH_PATTERN = rb'(\d+/\d+/((?:MDG1|EDC17|MED17|MG1|MD1|MEVD|ME)[^\x00/]{2,30})/(\d+)/([^\x00/]{2,20})//([^\x00/]{0,20})/)'

# Engine type + engine code (e.g. "R4 2.0l TDI  CFFB")
ENGINE_PATTERN = rb'([RVLI]\d\s+[\d.,]+[lL]\s+\w{2,6})\s+([A-Z]{3,4})'

# Bosch SW number + variant string (e.g. "1037394113P529TAYI")
BOSCH_SW_VARIANT_PATTERN = rb'(10[3-9]\d{7})([A-Z]\d{3}[A-Z0-9]{2,6})'

# Ford/Continental part number (e.g. "RK3A-12A650-AB")
FORD_PART_NUMBER_PATTERN = rb'(?<![A-Z0-9])([A-Z0-9]{4}-[A-Z0-9]{5,6}-[A-Z]{2})(?![A-Z0-9])'

# Ford calibration ID with prefix (e.g. "PXRK3A-12A650-AA")
FORD_CALIBRATION_PATTERN = rb'(?:Ford Motor Co\.\s*\d{4})([A-Z]{2}[A-Z0-9]{4}-[A-Z0-9]{5,6}-[A-Z]{2})'

# Continental ECU type identifiers (e.g. SID213, SID807, EMS3125)
CONTINENTAL_ECU_PATTERN = rb'(?:SID|EMS|SIM)\d{3,4}'
