"""Read metadata from ECU .bin files (Bosch, Continental/Ford, Delphi)."""

import re

from ecu_file_organizer.constants import (
    BOSCH_SW_PATTERN, OEM_NUMBER_PATTERN, ECU_TYPE_PATTERN, ENGINE_PATTERN,
    BOSCH_PATH_PATTERN, BOSCH_SW_VARIANT_PATTERN,
    FORD_PART_NUMBER_PATTERN, FORD_CALIBRATION_PATTERN,
    CONTINENTAL_ECU_PATTERN, CONTINENTAL_CAL_PATTERN, CONTINENTAL_BLOCK_PATTERN,
    DELPHI_CRD_PATTERN, DELPHI_DELIV_PATTERN,
    MERCEDES_PART_PATTERN, MERCEDES_PAIR_PATTERN,
    BOSCH_10SW_PATTERN, GM_CALIBRATION_PATTERN, DELCO_HW_PATTERN,
    VAG_ENGINE_CODE_PATTERN, PSA_PART_PATTERN
)


def read_bin_metadata(file_path: str) -> dict:
    """
    Read metadata from an ECU .bin file.

    Supports Bosch (EDC17, MED17, MD1, MG1, ...) and Continental/Ford (SID213, ...).
    Searches for ASCII strings embedded in the binary using regex patterns.

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

    # ===== BOSCH ECU detection =====

    # --- Bosch SW number + variant (e.g. "1037394113P529TAYI") ---
    m = re.search(BOSCH_SW_VARIANT_PATTERN, data)
    if m:
        try:
            result['bosch_sw_number'] = m.group(1).decode('ascii')
            result['bosch_variant'] = m.group(2).decode('ascii')
        except UnicodeDecodeError:
            pass

    # Fallback: plain Bosch SW number without variant
    if not result['bosch_sw_number']:
        m = re.search(BOSCH_SW_PATTERN, data)
        if m:
            try:
                result['bosch_sw_number'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- Bosch ECU type ---
    m = re.search(ECU_TYPE_PATTERN, data)
    if m:
        try:
            result['ecu_type'] = m.group(1).decode('ascii')
        except UnicodeDecodeError:
            pass

    # --- Bosch firmware version path ---
    m = re.search(BOSCH_PATH_PATTERN, data)
    if m:
        try:
            path_ecu = m.group(2).decode('ascii')
            path_variant = m.group(4).decode('ascii')
            path_id = m.group(5).decode('ascii')

            if not result['ecu_type']:
                result['ecu_type'] = path_ecu
            # Only use pure numeric calibration versions (e.g. 1396, 1110)
            # from the Bosch path - skip P-codes (P1070, P_1320) entirely
            if (not result['sw_version'] and path_variant
                    and path_variant.isdigit() and len(path_variant) >= 3):
                result['sw_version'] = path_variant
            if not result['bosch_sw_number'] and path_id:
                result['bosch_sw_number'] = path_id
        except UnicodeDecodeError:
            pass

    # --- Bosch 10SW software part number (e.g. "10SW017935") ---
    if not result['bosch_sw_number']:
        m = re.search(BOSCH_10SW_PATTERN, data)
        if m:
            try:
                result['bosch_sw_number'] = '10SW' + m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # ===== Continental/Ford detection =====

    # --- Ford part numbers (e.g. "RK3A-12A650-AB") ---
    ford_parts = re.findall(FORD_PART_NUMBER_PATTERN, data)
    if ford_parts:
        # Distinguish SW vs HW by middle segment:
        # xxAxxx = calibration/SW, xxCxxx = hardware module
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

        # Use first Ford SW part as sw_version if nothing found yet
        if ford_sw and not result['sw_version']:
            result['sw_version'] = ford_sw[0]

    # --- Ford calibration ID (e.g. "Ford Motor Co. 2025PXRK3A-12A650-AA") ---
    m = re.search(FORD_CALIBRATION_PATTERN, data)
    if m:
        try:
            cal_id = m.group(1).decode('ascii')
            if not result['bosch_sw_number']:
                result['bosch_sw_number'] = cal_id
        except UnicodeDecodeError:
            pass

    # --- Continental ECU type (SID213, EMS3125, etc.) ---
    if not result['ecu_type']:
        m = re.search(CONTINENTAL_ECU_PATTERN, data)
        if m:
            try:
                result['ecu_type'] = m.group(0).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- Continental/Siemens calibration ID (e.g. "CAFR1B00") ---
    if not result['sw_version']:
        m = re.search(CONTINENTAL_CAL_PATTERN, data)
        if m:
            try:
                result['sw_version'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- Continental calibration block OEM number (packed: "CARF8610RF86100010334254AA") ---
    if not result['oem_sw_number']:
        m = re.search(CONTINENTAL_BLOCK_PATTERN, data)
        if m:
            try:
                val = m.group(1).decode('ascii')
                if len(set(val[:8])) >= 4:
                    result['oem_sw_number'] = val
            except UnicodeDecodeError:
                pass

    # ===== Delco/ACDelco (GM) detection =====

    # --- Delco S-number hardware part (e.g. "S180161502A9") ---
    if not result['oem_hw_number']:
        m = re.search(DELCO_HW_PATTERN, data)
        if m:
            try:
                result['oem_hw_number'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- GM calibration number (e.g. "10214106AD") ---
    if not result['oem_sw_number']:
        for gm_match in re.finditer(GM_CALIBRATION_PATTERN, data):
            try:
                val = gm_match.group(1).decode('ascii')
                # Filter dummy values (all same digit like 22222222FU)
                digits = val[:8]
                if len(set(digits)) >= 4:
                    result['oem_sw_number'] = val
                    break
            except UnicodeDecodeError:
                continue

    # ===== Delphi CRD/CRD2 detection =====

    is_delphi_crd = False

    # --- Delphi CRD metadata (e.g. "CRD2-651-TMABDD11-639A4X-100kW-...") ---
    crd_matches = list(re.finditer(DELPHI_CRD_PATTERN, data))
    if crd_matches:
        is_delphi_crd = True
        # Use the last (most complete) CRD match
        m = crd_matches[-1]
        try:
            crd_ecu = m.group(1).decode('ascii')       # CRD or CRD2
            crd_sw = m.group(3).decode('ascii')         # calibration ID

            if not result['ecu_type']:
                result['ecu_type'] = crd_ecu
            if not result['sw_version']:
                result['sw_version'] = crd_sw

            # Clear false Bosch matches in Delphi files
            result['bosch_sw_number'] = ''
            result['bosch_variant'] = ''
        except UnicodeDecodeError:
            pass

    # --- Delphi calibration delivery ID (e.g. "SM05B006_DELIV_1 ...") ---
    if not result['sw_version']:
        deliv_matches = list(re.finditer(DELPHI_DELIV_PATTERN, data))
        if deliv_matches:
            is_delphi_crd = is_delphi_crd or True
            # Use the last DELIV match (usually the application SW, not bootloader)
            m = deliv_matches[-1]
            try:
                result['sw_version'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # ===== Mercedes part numbers (only for Delphi CRD files) =====

    if is_delphi_crd:
        # --- Mercedes A-number pairs (e.g. "A6511501879  A0054469640") ---
        # Try pair pattern first (handles packed data where single pattern fails)
        m = re.search(MERCEDES_PAIR_PATTERN, data)
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
            mb_parts = re.findall(MERCEDES_PART_PATTERN, data)
            for part_bytes in mb_parts:
                try:
                    val = part_bytes.decode('ascii')
                    if len(set(val[1:])) >= 4:
                        result['oem_hw_number'] = val
                        break
                except UnicodeDecodeError:
                    continue

    # ===== Generic patterns (all ECU brands) =====

    # --- VAG OEM part numbers ---
    oem_matches = re.findall(OEM_NUMBER_PATTERN, data)
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

    # --- Engine type + code ---
    m = re.search(ENGINE_PATTERN, data)
    if m:
        try:
            result['engine_type'] = m.group(1).decode('ascii')
            result['engine_code'] = m.group(2).decode('ascii')
        except UnicodeDecodeError:
            pass

    # Fallback: engine type without separate engine code (e.g. "R5 2,5L EDC")
    if not result['engine_type']:
        m = re.search(rb'([RVLI]\d\s+[\d.,]+[lL]\s+[A-Z]{2,6})', data)
        if m:
            try:
                result['engine_type'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- VAG engine code near controller address (e.g. "CAYCJ623") ---
    if not result['engine_code']:
        m = re.search(VAG_ENGINE_CODE_PATTERN, data)
        if m:
            try:
                result['engine_code'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- PSA part number from calibration context ---
    if not result['oem_sw_number']:
        m = re.search(PSA_PART_PATTERN, data)
        if m:
            try:
                result['oem_sw_number'] = m.group(1).decode('ascii')
            except UnicodeDecodeError:
                pass

    # --- SW version (Bosch: 4-5 digit number near OEM block) ---
    if not result['sw_version']:
        sw_version = _find_sw_version(data, oem_matches)
        if sw_version:
            result['sw_version'] = sw_version

    return result


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
