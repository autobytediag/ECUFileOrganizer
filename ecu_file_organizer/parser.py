"""Filename parser for Autotuner ECU file naming convention."""

from datetime import datetime

from ecu_file_organizer.constants import ECU_BRANDS


class FileParser:
    """Parse Autotuner filename format"""

    @staticmethod
    def parse_filename(filename):
        """
        Parse filename like: Volkswagen_Golf_2008__VI__1_6_TDI_CR_105_hp_Siemens_PCR2_1_OBD_NR.bin
        Returns dict with parsed fields
        """
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
