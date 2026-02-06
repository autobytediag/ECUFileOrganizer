"""Filename parser for Autotuner and Flex ECU file naming conventions."""

import os
import re
from datetime import datetime

from ecu_file_organizer.constants import (
    ECU_BRANDS, FLEX_BRANDS, FLEX_ECU_BRANDS, FLEX_READ_METHODS
)
from ecu_file_organizer.bin_reader import read_bin_metadata


class FileParser:
    """Parse Autotuner and Flex filename formats"""

    @staticmethod
    def is_flex_filename(filename):
        """Detect if filename uses Flex (dash-separated) naming convention."""
        name = filename.replace('.bin', '').replace('.BIN', '')
        # Flex files use dashes, not underscores as primary separator
        if '_' in name and '-' not in name:
            return False
        parts = name.split('-')
        if len(parts) < 4:
            return False
        # Check for known Flex brand prefix
        if parts[0].lower() in FLEX_BRANDS:
            return True
        # Check for known ECU brand as second part
        if len(parts) > 1 and parts[1].lower() in FLEX_ECU_BRANDS:
            return True
        return False

    @staticmethod
    def parse_flex_filename(filename):
        """
        Parse Flex filename format like:
          fomoco-delphi-dcm3.5-obd-maps-wf0lxxgcbldu74735-20260204104420.bin
          psa-bosch-md1cs003-tc298tp-bench-fullbackup-...-20260205122803-int-flash.bin
        Returns dict with parsed fields.
        """
        name = filename.replace('.bin', '').replace('.BIN', '')
        parts = name.split('-')

        parsed = {
            'make': '',
            'model': '',
            'engine': '',
            'ecu': '',
            'date': datetime.now().strftime('%Y%m%d'),
            'mileage': '',
            'registration': '',
            'read_method': ''
        }

        # Find timestamp (14 digits: YYYYMMDDHHmmSS)
        timestamp_idx = None
        for i, part in enumerate(parts):
            if len(part) == 14 and part.isdigit() and part[:2] == '20':
                timestamp_idx = i
                break

        if timestamp_idx is not None:
            parsed['date'] = parts[timestamp_idx][:8]

        # Parts before timestamp are the metadata
        meta_parts = parts[:timestamp_idx] if timestamp_idx else parts

        if not meta_parts:
            return parsed

        # First part: brand → make
        brand = meta_parts[0].lower()
        parsed['make'] = FLEX_BRANDS.get(brand, brand.capitalize())

        remaining = meta_parts[1:]

        # Second part: ECU brand (if recognized)
        ecu_brand = ''
        if remaining and remaining[0].lower() in FLEX_ECU_BRANDS:
            ecu_brand = remaining[0].capitalize()
            remaining = remaining[1:]

        # Next part: ECU type
        ecu_type = ''
        if remaining:
            ecu_type = remaining[0].upper()
            remaining = remaining[1:]

        # Find read method in remaining parts
        read_method_raw = ''
        for i, part in enumerate(remaining):
            if part.lower() in FLEX_READ_METHODS:
                read_method_raw = part.lower()
                break

        # Build ECU string
        ecu_str = f"{ecu_brand} {ecu_type}".strip()
        parsed['ecu'] = ecu_str

        # Map read method
        if read_method_raw == 'obd':
            parsed['read_method'] = 'Normal Read-OBD'
        elif read_method_raw == 'bench':
            parsed['read_method'] = 'Bench'
        elif read_method_raw == 'boot':
            parsed['read_method'] = 'Boot'

        return parsed

    @staticmethod
    def parse_filename(filename):
        """
        Parse filename - auto-detects Autotuner (underscore) or Flex (dash) format.
        Returns dict with parsed fields.
        """
        # Check for Flex format first
        if FileParser.is_flex_filename(filename):
            return FileParser.parse_flex_filename(filename)

        # Remove .bin extension
        name = filename.replace('.bin', '').replace('.BIN', '')

        # Split by underscores
        parts = name.split('_')

        parsed = {
            'make': '',
            'model': '',
            'engine': '',
            'ecu': '',
            'date': datetime.now().strftime('%Y%m%d'),
            'mileage': '',
            'registration': '',
            'read_method': ''
        }

        # Try to extract make (usually first part)
        if len(parts) > 0:
            parsed['make'] = parts[0]

        # Try to extract model (parts after make until double underscore or numbers)
        model_parts = []
        ecu_parts = []
        found_ecu = False

        for i, part in enumerate(parts[1:], 1):
            # Skip empty parts
            if not part:
                continue

            # Look for ECU indicators
            if any(ecu_brand in part for ecu_brand in ECU_BRANDS):
                found_ecu = True
                ecu_parts.append(part)
            elif found_ecu:
                ecu_parts.append(part)
            elif not any(x in part for x in ['OBD', 'BENCH', 'BOOT', 'NR', 'OR', 'hp', 'ps', 'kW']):
                model_parts.append(part)

        parsed['model'] = ' '.join(model_parts) if model_parts else ''
        parsed['ecu'] = ' '.join(ecu_parts) if ecu_parts else ''

        # Detect read method from filename
        name_upper = name.upper()
        if 'BENCH' in name_upper:
            parsed['read_method'] = 'Bench'
        elif 'BOOT' in name_upper:
            parsed['read_method'] = 'Boot'
        elif 'OBD' in name_upper:
            # Check if it might be virtual read
            if 'VIRTUAL' in name_upper or 'VR' in name_upper:
                parsed['read_method'] = 'Virtual Read-OBD'
            else:
                parsed['read_method'] = 'Normal Read-OBD'

        # Clean up ECU name
        parsed['ecu'] = parsed['ecu'].replace('  ', ' ').strip()

        return parsed

    @staticmethod
    def parse_bin_file(file_path):
        """
        Combine filename parsing with BIN file metadata extraction.

        Returns dict with all filename fields plus BIN metadata:
            sw_version, bosch_sw_number, oem_hw_number,
            oem_sw_number, engine_code
        """
        filename = os.path.basename(file_path)
        parsed = FileParser.parse_filename(filename)

        # Add empty BIN metadata fields as defaults
        parsed['sw_version'] = ''
        parsed['bosch_sw_number'] = ''
        parsed['oem_hw_number'] = ''
        parsed['oem_sw_number'] = ''
        parsed['engine_code'] = ''
        parsed['engine_type'] = ''

        # Try to read BIN metadata
        if file_path.lower().endswith('.bin') and os.path.isfile(file_path):
            bin_meta = read_bin_metadata(file_path)

            parsed['sw_version'] = bin_meta.get('sw_version', '')
            parsed['bosch_sw_number'] = bin_meta.get('bosch_sw_number', '')
            parsed['oem_hw_number'] = bin_meta.get('oem_hw_number', '')
            parsed['oem_sw_number'] = bin_meta.get('oem_sw_number', '')
            parsed['engine_code'] = bin_meta.get('engine_code', '')
            parsed['engine_type'] = bin_meta.get('engine_type', '')

            # If BIN has a better ECU type, use it (e.g. EDC17_C46 vs "Bosch EDC17C46")
            if bin_meta.get('ecu_type'):
                parsed['ecu'] = bin_meta['ecu_type']

        return parsed
