using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ECUFileOrganizer
{
    /// <summary>
    /// Read metadata from ECU .bin files.
    /// Supports Bosch, Continental/Siemens, Ford, Delphi, Delco/ACDelco,
    /// Transtron, BMW, Mercedes.
    /// </summary>
    static class BinReader
    {
        public static Dictionary<string, string> ReadMetadata(string filePath)
        {
            var result = new Dictionary<string, string>
            {
                ["sw_version"] = "",
                ["bosch_sw_number"] = "",
                ["bosch_variant"] = "",
                ["oem_hw_number"] = "",
                ["oem_sw_number"] = "",
                ["ecu_type"] = "",
                ["engine_type"] = "",
                ["engine_code"] = ""
            };

            byte[] data;
            try { data = File.ReadAllBytes(filePath); }
            catch { return result; }

            if (data.Length == 0) return result;

            // Convert binary to ASCII string for regex matching.
            // Non-ASCII bytes become '?' which won't interfere with patterns.
            string ascii = Encoding.ASCII.GetString(data);

            // Run each brand's extractor (order matters)
            bool isDelphiCrd = false;
            ExtractBosch(ascii, result);
            ExtractFord(ascii, result);
            ExtractContinental(ascii, result);
            ExtractDelphi(ascii, result, ref isDelphiCrd);
            ExtractDelco(ascii, result);
            ExtractTranstron(ascii, result);
            ExtractBmw(ascii, result);
            ExtractMercedes(ascii, result, isDelphiCrd);
            ExtractGeneric(ascii, result, isDelphiCrd);

            return result;
        }

        // ====================================================================
        // Brand-specific extractors
        // ====================================================================

        static void ExtractBosch(string data, Dictionary<string, string> r)
        {
            // SW number + variant (e.g. "1037394113P529TAYI")
            var m = BoschPatterns.SwVariant.Match(data);
            if (m.Success)
            {
                r["bosch_sw_number"] = m.Groups[1].Value;
                r["bosch_variant"] = m.Groups[2].Value;
            }

            // Fallback: plain SW number without variant
            if (r["bosch_sw_number"] == "")
            {
                m = BoschPatterns.SwNumber.Match(data);
                if (m.Success) r["bosch_sw_number"] = m.Groups[1].Value;
            }

            // ECU type (EDC17_C46, MD1CS003, etc.)
            m = BoschPatterns.EcuType.Match(data);
            if (m.Success) r["ecu_type"] = m.Groups[1].Value;

            // Firmware version path (e.g. "38/1/EDC17_C50/11/P_1320//VHKL0///")
            m = BoschPatterns.Path.Match(data);
            if (m.Success)
            {
                string pathEcu = m.Groups[2].Value;
                string pathVariant = m.Groups[4].Value;
                string pathId = m.Groups[5].Value;

                if (r["ecu_type"] == "") r["ecu_type"] = pathEcu;

                // Only use pure numeric calibration versions (e.g. 1396, 1110)
                // Skip P-codes (P1070, P_1320) - those are Bosch project IDs
                if (r["sw_version"] == "" && pathVariant.Length >= 3
                    && pathVariant.All(char.IsDigit))
                {
                    r["sw_version"] = pathVariant;
                }

                if (r["bosch_sw_number"] == "" && pathId != "")
                    r["bosch_sw_number"] = pathId;
            }

            // 10SW software part number (e.g. "10SW017935")
            if (r["bosch_sw_number"] == "")
            {
                m = BoschPatterns.TenSw.Match(data);
                if (m.Success) r["bosch_sw_number"] = "10SW" + m.Groups[1].Value;
            }

            // Bosch ECU hardware number (e.g. "0261206042", "0281014568")
            // Only search if we confirmed this is a Bosch ECU
            bool hasBosch = r["bosch_sw_number"] != "" || r["bosch_variant"] != ""
                || (r["ecu_type"] != "" && Regex.IsMatch(r["ecu_type"],
                    @"^(?:EDC|MED|MEVD|MG1|MD1|MDG1)"));
            if (r["oem_hw_number"] == "" && hasBosch)
            {
                m = BoschPatterns.HwNumber.Match(data);
                if (m.Success) r["oem_hw_number"] = m.Groups[1].Value;
            }
        }

        static void ExtractContinental(string data, Dictionary<string, string> r)
        {
            // ECU type (SID307, EMS3125, etc.)
            if (r["ecu_type"] == "")
            {
                var m = ContinentalPatterns.EcuType.Match(data);
                if (m.Success) r["ecu_type"] = m.Value;
            }

            // Calibration ID (e.g. "CAFR1B00", "CARF8610")
            if (r["sw_version"] == "")
            {
                var m = ContinentalPatterns.CalId.Match(data);
                if (m.Success) r["sw_version"] = m.Groups[1].Value;
            }

            // OEM cal number from packed block (e.g. "10334254AA")
            if (r["oem_sw_number"] == "")
            {
                var m = ContinentalPatterns.CalBlock.Match(data);
                if (m.Success)
                {
                    string val = m.Groups[1].Value;
                    if (val.Substring(0, 8).Distinct().Count() >= 4)
                        r["oem_sw_number"] = val;
                }
            }
        }

        static void ExtractFord(string data, Dictionary<string, string> r)
        {
            // Ford part numbers (e.g. "RK3A-12A650-AB", "BJ32-12K532-VLC")
            var matches = FordPatterns.PartNumber.Matches(data);
            if (matches.Count > 0)
            {
                var fordSw = new List<string>();
                var fordHw = new List<string>();

                foreach (Match pm in matches)
                {
                    string val = pm.Groups[1].Value;
                    string mid = val.Contains("-") ? val.Split('-')[1] : "";
                    if (mid.Length >= 3 && mid.Substring(0, 3).Contains("C"))
                        fordHw.Add(val);
                    else
                        fordSw.Add(val);
                }

                if (fordSw.Count > 0 && r["oem_sw_number"] == "")
                    r["oem_sw_number"] = fordSw[0];
                if (fordHw.Count > 0 && r["oem_hw_number"] == "")
                    r["oem_hw_number"] = fordHw[0];
                if (fordSw.Count > 0 && r["sw_version"] == "")
                    r["sw_version"] = fordSw[0];
            }

            // Ford calibration ID (e.g. "Ford Motor Co. 2025PX...")
            var m2 = FordPatterns.Calibration.Match(data);
            if (m2.Success && r["bosch_sw_number"] == "")
                r["bosch_sw_number"] = m2.Groups[1].Value;
        }

        static void ExtractDelphi(string data, Dictionary<string, string> r,
                                   ref bool isDelphiCrd)
        {
            // CRD metadata (e.g. "CRD2-651-TMABDD11-639A4X-100kW-...")
            var crdMatches = DelphiPatterns.Crd.Matches(data);
            if (crdMatches.Count > 0)
            {
                isDelphiCrd = true;
                var m = crdMatches[crdMatches.Count - 1]; // last match = most complete

                string crdEcu = m.Groups[1].Value;
                string crdEngine = m.Groups[2].Value;
                string crdSw = m.Groups[3].Value;

                if (r["ecu_type"] == "") r["ecu_type"] = crdEcu;
                if (r["sw_version"] == "") r["sw_version"] = crdSw;

                // Extract Mercedes engine family (651=OM651, 646=OM646, 642=OM642)
                if (r["engine_code"] == "" && crdEngine.All(char.IsDigit))
                    r["engine_code"] = "OM" + crdEngine;

                // Clear false Bosch matches in Delphi files
                r["bosch_sw_number"] = "";
                r["bosch_variant"] = "";
                r["oem_hw_number"] = "";

                // CRD power rating (e.g. "100kW", "125kW")
                if (r["engine_type"] == "")
                {
                    var mPower = DelphiPatterns.CrdPower.Match(data);
                    if (mPower.Success)
                        r["engine_type"] = mPower.Groups[1].Value + "kW";
                }
            }

            // DELIV calibration ID (e.g. "SM05B006_DELIV_1 Apr 11 ...")
            if (r["sw_version"] == "")
            {
                var delivMatches = DelphiPatterns.Deliv.Matches(data);
                if (delivMatches.Count > 0)
                {
                    isDelphiCrd = true;
                    r["sw_version"] = delivMatches[delivMatches.Count - 1].Groups[1].Value;
                }
            }

            // Engine code from DELIV extended (e.g. "T6C1HB05_DELIV_3_G9CD_ML6_...")
            if (r["engine_code"] == "")
            {
                var mExt = DelphiPatterns.DelivExt.Match(data);
                if (mExt.Success) r["engine_code"] = mExt.Groups[1].Value;
            }
        }

        static void ExtractDelco(string data, Dictionary<string, string> r)
        {
            // S-number hardware part (e.g. "S180161502A9")
            if (r["oem_hw_number"] == "")
            {
                var m = DelcoPatterns.HwPart.Match(data);
                if (m.Success) r["oem_hw_number"] = m.Groups[1].Value;
            }

            // GM calibration number (e.g. "10214106AD")
            if (r["oem_sw_number"] == "")
            {
                foreach (Match gm in DelcoPatterns.GmCal.Matches(data))
                {
                    string val = gm.Groups[1].Value;
                    if (val.Substring(0, 8).Distinct().Count() >= 4)
                    {
                        r["oem_sw_number"] = val;
                        break;
                    }
                }
            }
        }

        static void ExtractTranstron(string data, Dictionary<string, string> r)
        {
            // Only proceed if this is a Transtron ECU
            if (!TranstronPatterns.Copyright.IsMatch(data)) return;

            // Engine code + part number (e.g. "4JK1                z98250658")
            var m = TranstronPatterns.EnginePart.Match(data);
            if (m.Success)
            {
                if (r["engine_code"] == "") r["engine_code"] = m.Groups[1].Value;
                if (r["oem_sw_number"] == "") r["oem_sw_number"] = m.Groups[2].Value;
            }
        }

        static void ExtractBmw(string data, Dictionary<string, string> r)
        {
            // BMW engine code from DME/DDE string (e.g. "#B47D20O0-F10")
            if (r["engine_code"] == "")
            {
                var m = BmwPatterns.Engine.Match(data);
                if (m.Success) r["engine_code"] = m.Groups[1].Value;
            }
        }

        static void ExtractMercedes(string data, Dictionary<string, string> r,
                                     bool isDelphiCrd)
        {
            // Only active for Delphi CRD files
            if (!isDelphiCrd) return;

            // A-number pairs (e.g. "A6511501879  A0054469640")
            var m = MercedesPatterns.Pair.Match(data);
            if (m.Success)
            {
                string hw = m.Groups[1].Value;
                string sw = m.Groups[2].Value;
                if (r["oem_hw_number"] == "" && hw.Substring(1).Distinct().Count() >= 4)
                    r["oem_hw_number"] = hw;
                if (r["oem_sw_number"] == "" && sw.Substring(1).Distinct().Count() >= 4)
                    r["oem_sw_number"] = sw;
            }

            // Fallback: single A-numbers
            if (r["oem_hw_number"] == "")
            {
                foreach (Match am in MercedesPatterns.PartNumber.Matches(data))
                {
                    string val = am.Groups[1].Value;
                    if (val.Substring(1).Distinct().Count() >= 4)
                    {
                        r["oem_hw_number"] = val;
                        break;
                    }
                }
            }
        }

        static void ExtractGeneric(string data, Dictionary<string, string> r,
                                    bool isDelphiCrd)
        {
            // VAG OEM part numbers (e.g. "03L907309AE")
            var oemMatches = GenericPatterns.OemNumber.Matches(data);
            var hwCandidates = new List<string>();
            var swCandidates = new List<string>();

            foreach (Match om in oemMatches)
            {
                string val = om.Groups[1].Value;
                if (val.Contains("907"))
                    hwCandidates.Add(val);
                else if (val.Contains("906"))
                    swCandidates.Add(val);
                else
                {
                    if (hwCandidates.Count == 0) hwCandidates.Add(val);
                    else if (swCandidates.Count == 0) swCandidates.Add(val);
                }
            }

            if (hwCandidates.Count > 0 && r["oem_hw_number"] == "")
                r["oem_hw_number"] = hwCandidates[0];
            if (swCandidates.Count > 0 && r["oem_sw_number"] == "")
                r["oem_sw_number"] = swCandidates[0];

            // Engine type + code (e.g. "R4 2.0l TDI  CFFB")
            var me = GenericPatterns.Engine.Match(data);
            if (me.Success)
            {
                r["engine_type"] = me.Groups[1].Value;
                r["engine_code"] = me.Groups[2].Value;
            }

            // Fallback: engine type without separate code
            if (r["engine_type"] == "")
            {
                me = GenericPatterns.EngineType.Match(data);
                if (me.Success) r["engine_type"] = me.Groups[1].Value;
            }

            // VAG engine code near controller address (e.g. "CAYCJ623")
            if (r["engine_code"] == "")
            {
                me = GenericPatterns.VagEngine.Match(data);
                if (me.Success) r["engine_code"] = me.Groups[1].Value;
            }

            // PSA part number from calibration context
            if (r["oem_sw_number"] == "")
            {
                me = GenericPatterns.PsaPart.Match(data);
                if (me.Success) r["oem_sw_number"] = me.Groups[1].Value;
            }

            // OEM part number from Bosch path extension (PSA/Iveco)
            if (r["oem_sw_number"] == "")
            {
                me = GenericPatterns.BoschPathOem.Match(data);
                if (me.Success)
                {
                    string val = me.Groups[1].Value;
                    if (val.Distinct().Count() >= 4) r["oem_sw_number"] = val;
                }
            }

            // OEM SW number after .HEX calibration filename (Iveco/FPT)
            if (r["oem_sw_number"] == "")
            {
                me = GenericPatterns.HexOem.Match(data);
                if (me.Success) r["oem_sw_number"] = me.Groups[1].Value;
            }

            // SW version fallback: 4-5 digit number near OEM block
            if (r["sw_version"] == "")
                r["sw_version"] = FindSwVersion(data, oemMatches);

            // Delphi HW part number fallback (CRD files only)
            if (r["oem_hw_number"] == "" && isDelphiCrd)
            {
                foreach (Match dm in DelphiPatterns.HwPart.Matches(data))
                {
                    string val = dm.Groups[1].Value;
                    if (val.Distinct().Count() >= 4)
                    {
                        r["oem_hw_number"] = val;
                        break;
                    }
                }
            }
        }

        static string FindSwVersion(string data, MatchCollection oemMatches)
        {
            if (oemMatches.Count == 0) return "";

            int oemPos = oemMatches[0].Index;
            int start = Math.Max(0, oemPos - 2048);
            int end = Math.Min(data.Length, oemPos + 4096);
            string region = data.Substring(start, end - start);

            var versionPattern = new Regex(@"(?<![0-9A-Za-z])(\d{4,5})(?![0-9A-Za-z])");
            var oemStrings = new HashSet<string>();
            foreach (Match om in oemMatches)
                oemStrings.Add(om.Groups[1].Value);

            foreach (Match cm in versionPattern.Matches(region))
            {
                string val = cm.Groups[1].Value;
                if (oemStrings.Any(oem => oem.Contains(val))) continue;
                if (val.StartsWith("0")) continue;
                int num = int.Parse(val);
                if (num % 1000 == 0 || num < 1000) continue;
                return val;
            }

            return "";
        }
    }
}
