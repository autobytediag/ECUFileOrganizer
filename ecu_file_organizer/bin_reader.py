"""Read metadata from ECU .bin files.

Supports Bosch, Continental/Siemens, Ford, Delphi, Delco/ACDelco,
Transtron, BMW, Mercedes.

To add a new ECU brand:
  1. Add BRAND_PATTERNS dict to constants.py
  2. Create _extract_brand(data, result) function below
  3. Add the call to read_bin_metadata()
"""

import re

from ecu_file_organizer.constants import (
    BOSCH_PATTERNS, CONTINENTAL_PATTERNS, FORD_PATTERNS,
    DELPHI_PATTERNS, DELCO_PATTERNS, TRANSTRON_PATTERNS,
    BMW_PATTERNS, MERCEDES_PATTERNS, GENERIC_PATTERNS
)


def read_bin_metadata(file_path: str) -> dict:
    """
    Read metadata from an ECU .bin file.

    Searches for ASCII strings embedded in the binary using regex patterns.
    Each ECU brand has its own extraction function.

    Returns dict with:
        sw_version, bosch_sw_number, bosch_variant, oem_hw_number,
        oem_sw_number, ecu_type, engine_type, engine_code
    """
    result = {
        'sw_version': '',
        'bosch_sw_number': '',
        'bosch_variant': '',
        'oem_hw_number': '',
        'oem_sw_number': '',
        'ecu_type': '',
        'engine_type': '',
        'engine_code': '',
    }

    try:
        with open(file_path, 'rb') as f:
            data = f.read()
    except (OSError, IOError):
        return result

    if len(data) == 0:
        return result

    # Run each brand's extractor (order matters:
    #   - Ford before Continental: Ford part numbers take priority for sw_version
    #   - Delphi after Bosch: clears false Bosch matches in CRD files
    #   - Transtron/BMW after Bosch: only fill gaps left by Bosch)
    _extract_bosch(data, result)
    _extract_ford(data, result)
    _extract_continental(data, result)
    _extract_delphi(data, result)
    _extract_delco(data, result)
    _extract_transtron(data, result)
    _extract_bmw(data, result)
    _extract_mercedes(data, result)
    _extract_generic(data, result)

    return result


# ============================================================================
# Brand-specific extractors
# ============================================================================

def _extract_bosch(data: bytes, result: dict) -> None:
    """Extract metadata from Bosch ECU binaries (EDC17, MED17, MD1, MG1, ...)."""
    p = BOSCH_PATTERNS

    # --- SW number + variant (e.g. "1037394113P529TAYI") ---
    m = re.search(p['sw_variant'], data)
    if m:
        try:
            result['bosch_sw_number'] = m.group(1).decode('ascii')
            result['bosch_variant'] = m.group(2).decode('ascii')
        except UnicodeDecodeError:
            pass

    # Fallback: plain SW number without variant
    if not result['bosch_sw_number']:
        m = re.search(p['sw_number'], data)
        if m:
            try:
                result['bosch_sw_number'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- ECU type (EDC17_C46, MD1CS003, etc.) ---
    m = re.search(p['ecu_type'], data)
    if m:
        try:
            result['ecu_type'] = m.group(1).decode('ascii')
        except UnicodeDecodeError:
            pass

    # --- Firmware version path (e.g. "38/1/EDC17_C50/11/P_1320//VHKL0///") ---
    m = re.search(p['path'], data)
    if m:
        try:
            path_ecu = m.group(2).decode('ascii')
            path_variant = m.group(4).decode('ascii')
            path_id = m.group(5).decode('ascii')

            if not result['ecu_type']:
                result['ecu_type'] = path_ecu
            # Only use pure numeric calibration versions (e.g. 1396, 1110)
            # Skip P-codes (P1070, P_1320) - those are Bosch project IDs
            if (not result['sw_version'] and path_variant
                    and path_variant.isdigit() and len(path_variant) >= 3):
                result['sw_version'] = path_variant
            if not result['bosch_sw_number'] and path_id:
                result['bosch_sw_number'] = path_id
        except UnicodeDecodeError:
            pass

    # --- 10SW software part number (e.g. "10SW017935") ---
    if not result['bosch_sw_number']:
        m = re.search(p['10sw'], data)
        if m:
            try:
                result['bosch_sw_number'] = '10SW' + m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- Bosch ECU hardware number (e.g. "0261206042", "0281014568") ---
    # Only search if we confirmed this is a Bosch ECU (to avoid false positives)
    has_bosch = bool(
        result['bosch_sw_number'] or result['bosch_variant']
        or (result['ecu_type'] and re.match(
            r'(?:EDC|MED|MEVD|MG1|MD1|MDG1)', result['ecu_type']))
    )
    if not result['oem_hw_number'] and has_bosch:
        m = re.search(p['hw_number'], data)
        if m:
            try:
                result['oem_hw_number'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass


def _extract_continental(data: bytes, result: dict) -> None:
    """Extract metadata from Continental/Siemens ECUs (SID, EMS, PCR, ...)."""
    p = CONTINENTAL_PATTERNS

    # --- ECU type (SID307, EMS3125, etc.) ---
    if not result['ecu_type']:
        m = re.search(p['ecu_type'], data)
        if m:
            try:
                result['ecu_type'] = m.group(0).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- Calibration ID (e.g. "CAFR1B00", "CARF8610") ---
    if not result['sw_version']:
        m = re.search(p['cal_id'], data)
        if m:
            try:
                result['sw_version'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- OEM cal number from packed block (e.g. "CARF8610RF86100010334254AA") ---
    if not result['oem_sw_number']:
        m = re.search(p['cal_block'], data)
        if m:
            try:
                val = m.group(1).decode('ascii')
                if len(set(val[:8])) >= 4:  # filter dummy values
                    result['oem_sw_number'] = val
            except UnicodeDecodeError:
                pass


def _extract_ford(data: bytes, result: dict) -> None:
    """Extract metadata from Ford ECU binaries."""
    p = FORD_PATTERNS

    # --- Ford part numbers (e.g. "RK3A-12A650-AB") ---
    ford_parts = re.findall(p['part_number'], data)
    if ford_parts:
        # Distinguish SW vs HW by middle segment: xxCxxx = hardware module
        ford_sw = []
        ford_hw = []
        for part_bytes in ford_parts:
            try:
                val = part_bytes.decode('ascii')
            except UnicodeDecodeError:
                continue
            mid = val.split('-')[1] if '-' in val else ''
            if 'C' in mid[:3]:
                ford_hw.append(val)
            else:
                ford_sw.append(val)

        if ford_sw and not result['oem_sw_number']:
            result['oem_sw_number'] = ford_sw[0]
        if ford_hw and not result['oem_hw_number']:
            result['oem_hw_number'] = ford_hw[0]
        if ford_sw and not result['sw_version']:
            result['sw_version'] = ford_sw[0]

    # --- Ford calibration ID (e.g. "Ford Motor Co. 2025PXRK3A-12A650-AA") ---
    m = re.search(p['calibration'], data)
    if m:
        try:
            cal_id = m.group(1).decode('ascii')
            if not result['bosch_sw_number']:
                result['bosch_sw_number'] = cal_id
        except UnicodeDecodeError:
            pass


def _extract_delphi(data: bytes, result: dict) -> None:
    """Extract metadata from Delphi ECUs (CRD, CRD2, DCM, ...).

    Also sets result['_is_delphi_crd'] flag for Mercedes detection.
    Clears false Bosch matches when CRD is detected.
    """
    p = DELPHI_PATTERNS

    # --- CRD metadata (e.g. "CRD2-651-TMABDD11-639A4X-100kW-...") ---
    crd_matches = list(re.finditer(p['crd'], data))
    if crd_matches:
        result['_is_delphi_crd'] = True
        m = crd_matches[-1]  # last match is usually the most complete
        try:
            crd_ecu = m.group(1).decode('ascii')
            crd_engine = m.group(2).decode('ascii')
            crd_sw = m.group(3).decode('ascii')

            if not result['ecu_type']:
                result['ecu_type'] = crd_ecu
            if not result['sw_version']:
                result['sw_version'] = crd_sw

            # Extract Mercedes engine family (651=OM651, 646=OM646, 642=OM642)
            if not result['engine_code'] and crd_engine.isdigit():
                result['engine_code'] = 'OM' + crd_engine

            # Clear false Bosch matches in Delphi files
            result['bosch_sw_number'] = ''
            result['bosch_variant'] = ''
            # Clear potentially false Bosch/generic HW number
            # (Mercedes A-numbers will be extracted later by _extract_mercedes)
            result['oem_hw_number'] = ''
        except UnicodeDecodeError:
            pass

        # --- CRD power rating (e.g. "100kW", "125kW") ---
        if not result['engine_type']:
            m_power = re.search(p['crd_power'], data)
            if m_power:
                try:
                    power = m_power.group(1).decode('ascii')
                    result['engine_type'] = power + 'kW'
                except UnicodeDecodeError:
                    pass

    # --- DELIV calibration ID (e.g. "SM05B006_DELIV_1 Apr 11 ...") ---
    if not result['sw_version']:
        deliv_matches = list(re.finditer(p['deliv'], data))
        if deliv_matches:
            result['_is_delphi_crd'] = True
            m = deliv_matches[-1]  # last = application SW, not bootloader
            try:
                result['sw_version'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- Engine code from DELIV extended (e.g. "T6C1HB05_DELIV_3_G9CD_ML6_...") ---
    if not result['engine_code']:
        m = re.search(p['deliv_ext'], data)
        if m:
            try:
                result['engine_code'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass



def _extract_delco(data: bytes, result: dict) -> None:
    """Extract metadata from Delco/ACDelco (GM) ECUs (E98, etc.)."""
    p = DELCO_PATTERNS

    # --- S-number hardware part (e.g. "S180161502A9") ---
    if not result['oem_hw_number']:
        m = re.search(p['hw_part'], data)
        if m:
            try:
                result['oem_hw_number'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- GM calibration number (e.g. "10214106AD") ---
    if not result['oem_sw_number']:
        for gm_match in re.finditer(p['gm_cal'], data):
            try:
                val = gm_match.group(1).decode('ascii')
                if len(set(val[:8])) >= 4:  # filter dummy values
                    result['oem_sw_number'] = val
                    break
            except UnicodeDecodeError:
                continue


def _extract_transtron(data: bytes, result: dict) -> None:
    """Extract metadata from Transtron ECUs (Isuzu, Suzuki, etc.)."""
    p = TRANSTRON_PATTERNS

    # Only proceed if this is a Transtron ECU
    if not re.search(p['copyright'], data):
        return

    # --- Engine code + part number (e.g. "4JK1                z98250658") ---
    m = re.search(p['engine_part'], data)
    if m:
        try:
            if not result['engine_code']:
                result['engine_code'] = m.group(1).decode('ascii')
            if not result['oem_sw_number']:
                result['oem_sw_number'] = m.group(2).decode('ascii')
        except UnicodeDecodeError:
            pass


def _extract_bmw(data: bytes, result: dict) -> None:
    """Extract metadata from BMW ECU binaries (DME/DDE identification)."""
    p = BMW_PATTERNS

    # --- BMW engine code from DME/DDE string (e.g. "#B47D20O0-F10") ---
    if not result['engine_code']:
        m = re.search(p['engine'], data)
        if m:
            try:
                result['engine_code'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass


def _extract_mercedes(data: bytes, result: dict) -> None:
    """Extract Mercedes part numbers (only active for Delphi CRD files)."""
    if not result.get('_is_delphi_crd'):
        return

    p = MERCEDES_PATTERNS

    # --- A-number pairs (e.g. "A6511501879  A0054469640") ---
    m = re.search(p['pair'], data)
    if m:
        try:
            hw = m.group(1).decode('ascii')
            sw = m.group(2).decode('ascii')
            if not result['oem_hw_number'] and len(set(hw[1:])) >= 4:
                result['oem_hw_number'] = hw
            if not result['oem_sw_number'] and len(set(sw[1:])) >= 4:
                result['oem_sw_number'] = sw
        except UnicodeDecodeError:
            pass

    # Fallback: single A-numbers
    if not result['oem_hw_number']:
        for m in re.finditer(p['part_number'], data):
            try:
                val = m.group(1).decode('ascii')
                if len(set(val[1:])) >= 4:
                    result['oem_hw_number'] = val
                    break
            except UnicodeDecodeError:
                continue


def _extract_generic(data: bytes, result: dict) -> None:
    """Extract generic metadata (OEM numbers, engine info, SW version fallback)."""
    p = GENERIC_PATTERNS

    # --- VAG OEM part numbers (e.g. "03L907309AE") ---
    oem_matches = re.findall(p['oem_number'], data)
    hw_candidates = []
    sw_candidates = []

    for match_bytes in oem_matches:
        try:
            val = match_bytes.decode('ascii')
        except UnicodeDecodeError:
            continue
        if '907' in val:
            hw_candidates.append(val)
        elif '906' in val:
            sw_candidates.append(val)
        else:
            if not hw_candidates:
                hw_candidates.append(val)
            elif not sw_candidates:
                sw_candidates.append(val)

    if hw_candidates and not result['oem_hw_number']:
        result['oem_hw_number'] = hw_candidates[0]
    if sw_candidates and not result['oem_sw_number']:
        result['oem_sw_number'] = sw_candidates[0]

    # --- Engine type + code (e.g. "R4 2.0l TDI  CFFB") ---
    m = re.search(p['engine'], data)
    if m:
        try:
            result['engine_type'] = m.group(1).decode('ascii')
            result['engine_code'] = m.group(2).decode('ascii')
        except UnicodeDecodeError:
            pass

    # Fallback: engine type without separate code (e.g. "R5 2,5L EDC")
    if not result['engine_type']:
        m = re.search(p['engine_type'], data)
        if m:
            try:
                result['engine_type'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- VAG engine code near controller address (e.g. "CAYCJ623") ---
    if not result['engine_code']:
        m = re.search(p['vag_engine'], data)
        if m:
            try:
                result['engine_code'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- PSA part number from calibration context ---
    if not result['oem_sw_number']:
        m = re.search(p['psa_part'], data)
        if m:
            try:
                result['oem_sw_number'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- OEM part number from Bosch path extension (PSA/Iveco) ---
    if not result['oem_sw_number']:
        m = re.search(p['bosch_path_oem'], data)
        if m:
            try:
                val = m.group(1).decode('ascii')
                if len(set(val)) >= 4:  # filter dummy values
                    result['oem_sw_number'] = val
            except UnicodeDecodeError:
                pass

    # --- OEM SW number after .HEX calibration filename (Iveco/FPT) ---
    if not result['oem_sw_number']:
        m = re.search(p['hex_oem'], data)
        if m:
            try:
                result['oem_sw_number'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- SW version fallback: 4-5 digit number near OEM block ---
    if not result['sw_version']:
        sw_version = _find_sw_version(data, oem_matches)
        if sw_version:
            result['sw_version'] = sw_version

    # --- Delphi HW part number fallback (e.g. "28208056") ---
    # Only for CRD files where no Mercedes A-number or other HW was found
    if not result['oem_hw_number'] and result.get('_is_delphi_crd'):
        from ecu_file_organizer.constants import DELPHI_PATTERNS
        for m in re.finditer(DELPHI_PATTERNS['hw_part'], data):
            try:
                val = m.group(1).decode('ascii')
                if len(set(val)) >= 4:  # filter dummy values
                    result['oem_hw_number'] = val
                    break
            except UnicodeDecodeError:
                continue

    # Clean up internal flags
    result.pop('_is_delphi_crd', None)


def _find_sw_version(data: bytes, oem_matches: list) -> str:
    """
    Find the software version number in the metadata block.

    The SW version is typically a 4-5 digit number located near the OEM
    part numbers in the calibration data region.
    """
    if not oem_matches:
        return ''

    first_oem = oem_matches[0]
    oem_pos = data.find(first_oem)
    if oem_pos < 0:
        return ''

    start = max(0, oem_pos - 2048)
    end = min(len(data), oem_pos + 4096)
    region = data[start:end]

    version_pattern = rb'(?<![0-9A-Za-z])(\d{4,5})(?![0-9A-Za-z])'
    candidates = re.findall(version_pattern, region)

    oem_strings = set()
    for m in oem_matches:
        try:
            oem_strings.add(m.decode('ascii'))
        except UnicodeDecodeError:
            pass

    for candidate in candidates:
        try:
            val = candidate.decode('ascii')
        except UnicodeDecodeError:
            continue

        if any(val in oem_str for oem_str in oem_strings):
            continue
        if val.startswith('0'):
            continue
        num = int(val)
        if num % 1000 == 0:
            continue
        if num < 1000:
            continue

        return val

    return ''
