"""Shared constants for ECU File Organizer."""

APP_VERSION = "1.23"
APP_NAME = "ECU File Organizer"
APP_DISPLAY_NAME = f"ECU File Organizer v{APP_VERSION}"
SUPPORT_URL = "https://buymeacoffee.com/autobyte"

ECU_BRANDS = [
    'Bosch', 'Siemens', 'Delphi', 'Continental',
    'Marelli', 'Valeo', 'Denso', 'PCR', 'EDC', 'MED',
    'Delco', 'Transtron',
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

# Delphi CRD metadata string (e.g. "CRD2-651-TMABDD11-639A4X-100kW-...")
DELPHI_CRD_PATTERN = rb'(CRD\d?)-(\d{3})-([A-Z0-9]+)'

# Delphi calibration delivery identifier (e.g. "SM05B006_DELIV_1 Apr 11 10:42:37 2008")
DELPHI_DELIV_PATTERN = rb'([A-Z0-9]{4,15})_DELIV_\d'

# Mercedes OEM part number (e.g. "A6511501879")
MERCEDES_PART_PATTERN = rb'(?<![A-Z0-9])(A\d{10})(?!\d)'

# Bosch 10SW software part number (e.g. "10SW017935")
BOSCH_10SW_PATTERN = rb'10SW(\d{6})'

# Continental/Siemens calibration ID (e.g. "CAFR1B00", "CARF8610", "CARFE9M0")
# 3rd char must be R or F to avoid matching CAS/CAD/etc. prefixes
CONTINENTAL_CAL_PATTERN = rb'(CA[RF][A-Z0-9]{5})'

# Continental calibration block: cal_id + ref_id + OEM cal number (packed together)
# e.g. "CARF8610RF86100010334254AA" -> cal=CARF8610, oem=10334254AA
CONTINENTAL_BLOCK_PATTERN = rb'CA[RF][A-Z0-9]{5}[A-Z0-9]{6,8}(\d{8}[A-Z]{2})'

# GM/Delco calibration number (e.g. "10214106AD") - 8 digits + 2 letters
GM_CALIBRATION_PATTERN = rb'(?<![0-9])(\d{8}[A-Z]{2})(?![A-Z0-9])'

# Continental/Delco S-number hardware part (e.g. "S180161502A9")
DELCO_HW_PATTERN = rb'(S\d{9}[A-Z]\d)'

# VAG engine code near controller address (e.g. "CAYCJ623" -> engine code CAYC)
VAG_ENGINE_CODE_PATTERN = rb'([A-Z]{4})J623'

# PSA part number (10 digits in FOS calibration context)
PSA_PART_PATTERN = rb'(?:FOS|PSAAPP|PSA_)[^\x00]{0,80}(\d{10})'

# Mercedes A-number pair (e.g. "A6511501879  A0054469640")
MERCEDES_PAIR_PATTERN = rb'(A\d{10})\s+(A\d{10})'

# Flex tool filename brand mapping
FLEX_BRANDS = {
    'fomoco': 'Ford', 'psa': 'PSA', 'vag': 'Volkswagen',
    'bmw': 'BMW', 'fca': 'FCA', 'renault': 'Renault',
    'nissan': 'Nissan', 'opel': 'Opel', 'toyota': 'Toyota',
    'hyundai': 'Hyundai', 'kia': 'Kia', 'mercedes': 'Mercedes',
    'gm': 'GM', 'jlr': 'JLR', 'suzuki': 'Suzuki',
    'subaru': 'Subaru', 'mazda': 'Mazda', 'mitsubishi': 'Mitsubishi',
    'volvo': 'Volvo', 'iveco': 'Iveco', 'man': 'MAN',
    'daf': 'DAF', 'scania': 'Scania', 'isuzu': 'Isuzu',
    'honda': 'Honda', 'jaguar': 'Jaguar', 'landrover': 'Land Rover',
    'porsche': 'Porsche', 'audi': 'Audi', 'seat': 'Seat',
    'skoda': 'Skoda', 'chrysler': 'Chrysler', 'jeep': 'Jeep',
    'dodge': 'Dodge', 'alfa': 'Alfa Romeo', 'fiat': 'Fiat',
    'lancia': 'Lancia', 'citroen': 'Citroen', 'peugeot': 'Peugeot',
    'ds': 'DS', 'mini': 'Mini', 'smart': 'Smart',
}

# Known ECU manufacturer names in Flex filenames
FLEX_ECU_BRANDS = {
    'bosch', 'siemens', 'delphi', 'continental', 'marelli',
    'denso', 'delco', 'valeo', 'hitachi', 'kefico', 'transtron',
}

# Known read methods in Flex filenames
FLEX_READ_METHODS = {'obd', 'bench', 'boot'}
