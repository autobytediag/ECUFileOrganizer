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

# ============================================================================
# BIN metadata extraction patterns - grouped by ECU brand
#
# To add a new ECU brand:
#   1. Add a BRAND_PATTERNS dict here
#   2. Create _extract_brand() in bin_reader.py
#   3. Call _extract_brand(data, result) in read_bin_metadata()
# ============================================================================

BOSCH_PATTERNS = {
    # SW number + variant string (e.g. "1037394113P529TAYI")
    'sw_variant':  rb'(10[3-9]\d{7})([A-Z]\d{3}[A-Z0-9]{2,6})',
    # SW number alone (10 digits, starts with 103x/109x)
    'sw_number':   rb'(10[3-9]\d{7})',
    # 10SW software part number (e.g. "10SW017935")
    '10sw':        rb'10SW(\d{6})',
    # ECU type identifiers (EDC17_C46, MD1CS003, MED17_7, etc.)
    'ecu_type':    rb'(MDG1_MD1CS\d{3}|MD1CS\d{3}|EDC\d+_[A-Z]?\d+|MED\d+_[A-Z]?\d+|MEVD\d+_[A-Z]?\d+|MG1_[A-Z]{1,2}\d+)',
    # Firmware version path (e.g. "38/1/EDC17_C50/11/P_1320//VHKL0///")
    'path':        rb'(\d+/\d+/((?:MDG1|EDC17|MED17|MG1|MD1|MEVD|ME)[^\x00/]{2,30})/(\d+)/([^\x00/]{2,20})//([^\x00/]{0,20})/)',
    # Bosch ECU hardware part number (e.g. "0261206042" gasoline, "0281014568" diesel)
    'hw_number':   rb'(?<!\d)(02[68]1\d{6})(?!\d)',
}

CONTINENTAL_PATTERNS = {
    # ECU type identifiers (SID307, EMS3125, PCR2.1, etc.)
    'ecu_type':    rb'(?:SID|EMS|SIM|PCR)[\d.]{2,5}',
    # Calibration ID (e.g. "CAFR1B00", "CARF8610") - 3rd char R or F
    'cal_id':      rb'(CA[RF][A-Z0-9]{5})',
    # Calibration block: cal_id + ref_id + OEM cal number (packed)
    # e.g. "CARF8610RF86100010334254AA" -> oem=10334254AA
    'cal_block':   rb'CA[RF][A-Z0-9]{5}[A-Z0-9]{6,8}(\d{8}[A-Z]{2})',
}

FORD_PATTERNS = {
    # Part number (e.g. "RK3A-12A650-AB", "BV61-14C204-FK")
    'part_number': rb'(?<![A-Z0-9])([A-Z0-9]{4}-[A-Z0-9]{5,6}-[A-Z]{2})(?![A-Z0-9])',
    # Calibration ID with Ford prefix (e.g. "Ford Motor Co. 2025PXRK3A-12A650-AA")
    'calibration': rb'(?:Ford Motor Co\.\s*\d{4})([A-Z]{2}[A-Z0-9]{4}-[A-Z0-9]{5,6}-[A-Z]{2})',
}

DELPHI_PATTERNS = {
    # CRD metadata string (e.g. "CRD2-651-TMABDD11-639A4X-100kW-...")
    'crd':         rb'(CRD\d?)-(\d{3})-([A-Z0-9]+)',
    # CRD extended: extract power rating (e.g. "100kW", "125kW")
    'crd_power':   rb'CRD\d?-\d{3}-[A-Z0-9]+-[A-Z0-9]+-(\d{2,3})[kK][wW]',
    # Calibration delivery identifier (e.g. "SM05B006_DELIV_1 Apr 11 ...")
    'deliv':       rb'([A-Z0-9]{4,15})_DELIV_\d',
    # Delphi internal HW part number (e.g. "28208056", "28292738")
    'hw_part':     rb'(?<!\d)(28[2-4]\d{5})(?!\d)',
}

DELCO_PATTERNS = {
    # S-number hardware part (e.g. "S180161502A9")
    'hw_part':     rb'(S\d{9}[A-Z]\d)',
    # GM calibration number (e.g. "10214106AD") - 8 digits + 2 letters
    'gm_cal':      rb'(?<![0-9])(\d{8}[A-Z]{2})(?![A-Z0-9])',
}

MERCEDES_PATTERNS = {
    # OEM part number (e.g. "A6511501879")
    'part_number': rb'(?<![A-Z0-9])(A\d{10})(?!\d)',
    # A-number pair (e.g. "A6511501879  A0054469640")
    'pair':        rb'(A\d{10})\s+(A\d{10})',
}

GENERIC_PATTERNS = {
    # VAG OEM part numbers (e.g. "03L907309AE") with boundary assertions
    'oem_number':  rb'(?<![0-9A-Za-z])(\d{2,3}[A-Z]\d{6}[A-Z]{0,3})(?![0-9A-Za-z])',
    # Engine type + engine code (e.g. "R4 2.0l TDI  CFFB")
    'engine':      rb'([RVLI]\d\s+[\d.,]+[lL]\s+\w{2,6})\s+([A-Z]{3,4})',
    # Engine type without code (e.g. "R5 2,5L EDC")
    'engine_type': rb'([RVLI]\d\s+[\d.,]+[lL]\s+[A-Z]{2,6})',
    # VAG engine code near controller address (e.g. "CAYCJ623" -> CAYC)
    'vag_engine':  rb'([A-Z]{4})J623',
    # PSA part number in FOS calibration context
    'psa_part':    rb'(?:FOS|PSAAPP|PSA_)[^\x00]{0,80}(\d{10})',
}

# ============================================================================
# Flex tool filename constants
# ============================================================================

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

FLEX_ECU_BRANDS = {
    'bosch', 'siemens', 'delphi', 'continental', 'marelli',
    'denso', 'delco', 'valeo', 'hitachi', 'kefico', 'transtron',
}

FLEX_READ_METHODS = {'obd', 'bench', 'boot'}
