"""Read metadata from Bosch .bin ECU files."""

import re

from ecu_file_organizer.constants import (
    BOSCH_SW_PATTERN, OEM_NUMBER_PATTERN, ECU_TYPE_PATTERN, ENGINE_PATTERN,
    BOSCH_PATH_PATTERN, BOSCH_SW_VARIANT_PATTERN
)


def read_bin_metadata(file_path: str) -> dict:
    """
    Read metadata from a Bosch .bin file.

    Searches for ASCII strings embedded in the binary using regex patterns.
    Works across different ECU variants (no fixed offsets).

    Returns dict with:
        sw_version, bosch_sw_number, oem_hw_number,
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

    # --- ECU type ---
    m = re.search(ECU_TYPE_PATTERN, data)
    if m:
        try:
            result['ecu_type'] = m.group(1).decode('ascii')
        except UnicodeDecodeError:
            pass

    # --- Bosch firmware version path ---
    # Format: "38/1/MDG1_MD1CS001/11/P_1401//VLWT0///"
    # Provides ECU type, variant, and an internal identifier
    m = re.search(BOSCH_PATH_PATTERN, data)
    if m:
        try:
            path_ecu = m.group(2).decode('ascii')       # e.g. "MDG1_MD1CS001"
            path_variant = m.group(4).decode('ascii')    # e.g. "P_1401"
            path_id = m.group(5).decode('ascii')         # e.g. "VLWT0"

            # Use path ECU type if no direct ECU type found yet
            if not result['ecu_type']:
                result['ecu_type'] = path_ecu

            # Store variant as SW version if not found otherwise
            if not result['sw_version'] and path_variant:
                result['sw_version'] = path_variant

            # Store internal ID as Bosch SW number if not found
            if not result['bosch_sw_number'] and path_id:
                result['bosch_sw_number'] = path_id
        except UnicodeDecodeError:
            pass

    # --- OEM part numbers ---
    # Collect all matches; distinguish HW vs SW by common prefixes.
    # Typically: xxL907xxx = HW number, xxL906xxx = SW number (VAG convention)
    oem_matches = re.findall(OEM_NUMBER_PATTERN, data)
    hw_candidates = []
    sw_candidates = []

    for match_bytes in oem_matches:
        try:
            val = match_bytes.decode('ascii')
        except UnicodeDecodeError:
            continue

        # VAG convention: 907 in middle = hardware, 906 = software
        if '907' in val:
            hw_candidates.append(val)
        elif '906' in val:
            sw_candidates.append(val)
        else:
            # Generic fallback: first seen = HW, second = SW
            if not hw_candidates:
                hw_candidates.append(val)
            elif not sw_candidates:
                sw_candidates.append(val)

    if hw_candidates:
        result['oem_hw_number'] = hw_candidates[0]
    if sw_candidates:
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

    # --- SW version ---
    # Look for a 4-5 digit isolated number near the OEM numbers block.
    # Strategy: find the region around the first OEM match and search there.
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

    # Find position of first OEM number to locate the metadata region
    first_oem = oem_matches[0]
    oem_pos = data.find(first_oem)
    if oem_pos < 0:
        return ''

    # Search in a window around the OEM numbers (±4KB)
    start = max(0, oem_pos - 2048)
    end = min(len(data), oem_pos + 4096)
    region = data[start:end]

    # Look for isolated 4-5 digit numbers (not part of larger numbers/strings)
    # These are typically SW version identifiers
    version_pattern = rb'(?<![0-9A-Za-z])(\d{4,5})(?![0-9A-Za-z])'
    candidates = re.findall(version_pattern, region)

    # Filter out numbers that are clearly not versions:
    # - OEM numbers themselves
    # - Numbers starting with 0
    # - Very round numbers (10000, 20000, etc.)
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

        # Skip if it's part of an OEM number
        if any(val in oem_str for oem_str in oem_strings):
            continue

        # Skip leading zeros
        if val.startswith('0'):
            continue

        # Skip very round numbers (likely addresses/offsets)
        num = int(val)
        if num % 1000 == 0:
            continue

        # Skip numbers that are too small to be a version
        if num < 1000:
            continue

        return val

    return ''
