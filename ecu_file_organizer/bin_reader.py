"""Read metadata from ECU .bin files (Bosch, Continental/Ford)."""

import re

from ecu_file_organizer.constants import (
    BOSCH_SW_PATTERN, OEM_NUMBER_PATTERN, ECU_TYPE_PATTERN, ENGINE_PATTERN,
    BOSCH_PATH_PATTERN, BOSCH_SW_VARIANT_PATTERN,
    FORD_PART_NUMBER_PATTERN, FORD_CALIBRATION_PATTERN,
    CONTINENTAL_ECU_PATTERN
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
            if not result['sw_version'] and path_variant:
                result['sw_version'] = path_variant
            if not result['bosch_sw_number'] and path_id:
                result['bosch_sw_number'] = path_id
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
