using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ECUFileOrganizer
{
    /// <summary>Filename parser for Autotuner and Flex ECU file naming conventions.</summary>
    static class FileParser
    {
        public static bool IsFlexFilename(string filename)
        {
            string name = filename.Replace(".bin", "").Replace(".BIN", "");
            if (name.Contains("_") && !name.Contains("-")) return false;
            string[] parts = name.Split('-');
            if (parts.Length < 4) return false;
            if (FlexConstants.Brands.ContainsKey(parts[0].ToLower())) return true;
            if (parts.Length > 1 && FlexConstants.EcuBrands.Contains(parts[1].ToLower())) return true;
            return false;
        }

        public static Dictionary<string, string> ParseFlexFilename(string filename)
        {
            string name = filename.Replace(".bin", "").Replace(".BIN", "");
            string[] parts = name.Split('-');

            var parsed = NewParsedDict();

            // Find timestamp (14 digits: YYYYMMDDHHmmSS)
            int? timestampIdx = null;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 14 && parts[i].All(char.IsDigit)
                    && parts[i].StartsWith("20"))
                {
                    timestampIdx = i;
                    break;
                }
            }

            if (timestampIdx.HasValue)
                parsed["date"] = parts[timestampIdx.Value].Substring(0, 8);

            // Parts before timestamp are the metadata
            var metaParts = timestampIdx.HasValue
                ? parts.Take(timestampIdx.Value).ToArray()
                : parts;

            if (metaParts.Length == 0) return parsed;

            // First part: brand -> make
            string brand = metaParts[0].ToLower();
            parsed["make"] = FlexConstants.Brands.ContainsKey(brand)
                ? FlexConstants.Brands[brand]
                : char.ToUpper(brand[0]) + brand.Substring(1);

            var remaining = metaParts.Skip(1).ToList();

            // Second part: ECU brand (if recognized)
            string ecuBrand = "";
            if (remaining.Count > 0 && FlexConstants.EcuBrands.Contains(remaining[0].ToLower()))
            {
                ecuBrand = char.ToUpper(remaining[0][0]) + remaining[0].Substring(1).ToLower();
                remaining.RemoveAt(0);
            }

            // Next part: ECU type
            string ecuType = "";
            if (remaining.Count > 0)
            {
                ecuType = remaining[0].ToUpper();
                remaining.RemoveAt(0);
            }

            // Find read method in remaining parts
            string readMethodRaw = "";
            foreach (string part in remaining)
            {
                if (FlexConstants.ReadMethods.Contains(part.ToLower()))
                {
                    readMethodRaw = part.ToLower();
                    break;
                }
            }

            // Build ECU string
            parsed["ecu"] = $"{ecuBrand} {ecuType}".Trim();

            // Map read method
            if (readMethodRaw == "obd") parsed["read_method"] = "Normal Read-OBD";
            else if (readMethodRaw == "bench") parsed["read_method"] = "Bench";
            else if (readMethodRaw == "boot") parsed["read_method"] = "Boot";

            return parsed;
        }

        public static Dictionary<string, string> ParseFilename(string filename)
        {
            // Check for Flex format first
            if (IsFlexFilename(filename))
                return ParseFlexFilename(filename);

            // Remove .bin extension
            string name = filename.Replace(".bin", "").Replace(".BIN", "");
            string[] parts = name.Split('_');

            var parsed = NewParsedDict();

            // Try to extract make (usually first part)
            if (parts.Length > 0)
                parsed["make"] = parts[0];

            // Try to extract model, ECU, read method
            var modelParts = new List<string>();
            var ecuParts = new List<string>();
            bool foundEcu = false;

            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                if (string.IsNullOrEmpty(part)) continue;

                if (Constants.EcuBrands.Any(b => part.Contains(b)))
                {
                    foundEcu = true;
                    ecuParts.Add(part);
                }
                else if (foundEcu)
                {
                    ecuParts.Add(part);
                }
                else if (!new[] { "OBD", "BENCH", "BOOT", "NR", "OR", "hp", "ps", "kW" }
                    .Any(x => part.Contains(x)))
                {
                    modelParts.Add(part);
                }
            }

            parsed["model"] = string.Join(" ", modelParts);
            parsed["ecu"] = string.Join(" ", ecuParts);

            // Detect read method
            string nameUpper = name.ToUpper();
            if (nameUpper.Contains("BENCH"))
                parsed["read_method"] = "Bench";
            else if (nameUpper.Contains("BOOT"))
                parsed["read_method"] = "Boot";
            else if (nameUpper.Contains("OBD"))
            {
                if (nameUpper.Contains("VIRTUAL") || nameUpper.Contains("VR"))
                    parsed["read_method"] = "Virtual Read-OBD";
                else
                    parsed["read_method"] = "Normal Read-OBD";
            }

            // Clean up ECU name
            parsed["ecu"] = parsed["ecu"].Replace("  ", " ").Trim();

            return parsed;
        }

        public static Dictionary<string, string> ParseBinFile(string filePath)
        {
            string filename = Path.GetFileName(filePath);
            var parsed = ParseFilename(filename);

            // Add empty BIN metadata fields as defaults
            parsed["sw_version"] = "";
            parsed["bosch_sw_number"] = "";
            parsed["oem_hw_number"] = "";
            parsed["oem_sw_number"] = "";
            parsed["engine_code"] = "";
            parsed["engine_type"] = "";

            // Try to read BIN metadata
            if (filePath.ToLower().EndsWith(".bin") && File.Exists(filePath))
            {
                var binMeta = BinReader.ReadMetadata(filePath);

                parsed["sw_version"] = binMeta["sw_version"];
                parsed["bosch_sw_number"] = binMeta["bosch_sw_number"];
                parsed["oem_hw_number"] = binMeta["oem_hw_number"];
                parsed["oem_sw_number"] = binMeta["oem_sw_number"];
                parsed["engine_code"] = binMeta["engine_code"];
                parsed["engine_type"] = binMeta["engine_type"];

                // If BIN has a better ECU type, use it
                if (binMeta["ecu_type"] != "")
                    parsed["ecu"] = binMeta["ecu_type"];
            }

            return parsed;
        }

        static Dictionary<string, string> NewParsedDict()
        {
            return new Dictionary<string, string>
            {
                ["make"] = "",
                ["model"] = "",
                ["engine"] = "",
                ["ecu"] = "",
                ["date"] = DateTime.Now.ToString("yyyyMMdd"),
                ["mileage"] = "",
                ["registration"] = "",
                ["read_method"] = ""
            };
        }
    }
}
