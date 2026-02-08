using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ECUFileOrganizer
{
    /// <summary>Shared constants for ECU File Organizer.</summary>
    static class Constants
    {
        public const string AppVersion = "1.23";
        public const string AppName = "ECU File Organizer";
        public static readonly string AppDisplayName = $"ECU File Organizer v{AppVersion}";
        public const string SupportUrl = "https://www.buymeacoffee.com/sn0w3y";

        public static readonly string[] EcuBrands =
        {
            "Bosch", "Siemens", "Delphi", "Continental",
            "Marelli", "Valeo", "Denso", "PCR", "EDC", "MED",
            "Delco", "Transtron"
        };

        public static readonly string[] ReadMethods =
        {
            "Normal Read-OBD",
            "Virtual Read-OBD",
            "Bench",
            "Boot"
        };
    }

    // ========================================================================
    // BIN metadata extraction patterns - grouped by ECU brand
    // ========================================================================

    /// <summary>Bosch ECU patterns (EDC17, MED17, MD1, MG1, ...).</summary>
    static class BoschPatterns
    {
        // SW number + variant string (e.g. "1037394113P529TAYI")
        public static readonly Regex SwVariant =
            new Regex(@"(10[3-9]\d{7})([A-Z]\d{3}[A-Z0-9]{2,6})", RegexOptions.Compiled);

        // SW number alone (10 digits, starts with 103x-109x)
        public static readonly Regex SwNumber =
            new Regex(@"(10[3-9]\d{7})", RegexOptions.Compiled);

        // 10SW software part number (e.g. "10SW017935")
        public static readonly Regex TenSw =
            new Regex(@"10SW(\d{6})", RegexOptions.Compiled);

        // ECU type identifiers (EDC17_C46, MD1CS003, MED17_7, etc.)
        public static readonly Regex EcuType =
            new Regex(@"(MDG1_MD1CS\d{3}|MD1CS\d{3}|EDC\d+_[A-Z]?\d+|MED\d+_[A-Z]?\d+|MEVD\d+_[A-Z]?\d+|MG1_[A-Z]{1,2}\d+)",
                RegexOptions.Compiled);

        // Firmware version path (e.g. "38/1/EDC17_C50/11/P_1320//VHKL0///")
        public static readonly Regex Path =
            new Regex(@"(\d+/\d+/((?:MDG1|EDC17|MED17|MG1|MD1|MEVD|ME)[^\0/]{2,30})/(\d+)/([^\0/]{2,20})//([^\0/]{0,20})/)",
                RegexOptions.Compiled);

        // Bosch ECU hardware part number (e.g. "0261206042" gasoline, "0281014568" diesel)
        public static readonly Regex HwNumber =
            new Regex(@"(?<!\d)(02[68]1\d{6})(?!\d)", RegexOptions.Compiled);
    }

    /// <summary>Continental/Siemens ECU patterns (SID, EMS, PCR, ...).</summary>
    static class ContinentalPatterns
    {
        // ECU type identifiers (SID307, EMS3125, PCR2.1, etc.)
        public static readonly Regex EcuType =
            new Regex(@"(?:SID|EMS|SIM|PCR)[\d.]{2,5}", RegexOptions.Compiled);

        // Calibration ID (e.g. "CAFR1B00", "CARF8610") - 3rd char R or F
        public static readonly Regex CalId =
            new Regex(@"(CA[RF][A-Z0-9]{5})", RegexOptions.Compiled);

        // Calibration block: cal_id + ref_id + OEM cal number (packed)
        public static readonly Regex CalBlock =
            new Regex(@"CA[RF][A-Z0-9]{5}[A-Z0-9]{6,8}(\d{8}[A-Z]{2})", RegexOptions.Compiled);
    }

    /// <summary>Ford/JLR ECU patterns.</summary>
    static class FordPatterns
    {
        // Part number (e.g. "RK3A-12A650-AB", "BJ32-12K532-VLC")
        // 2-letter suffix for Ford, 3-letter for JLR (Jaguar/Land Rover)
        public static readonly Regex PartNumber =
            new Regex(@"(?<![A-Z0-9])([A-Z0-9]{4}-[A-Z0-9]{5,6}-[A-Z]{2,3})(?![A-Z0-9])", RegexOptions.Compiled);

        // Calibration ID with Ford prefix (e.g. "Ford Motor Co. 2025PXRK3A-12A650-AA")
        public static readonly Regex Calibration =
            new Regex(@"(?:Ford Motor Co\.\s*\d{4})([A-Z]{2}[A-Z0-9]{4}-[A-Z0-9]{5,6}-[A-Z]{2})", RegexOptions.Compiled);
    }

    /// <summary>Delphi ECU patterns (CRD, CRD2, DCM, ...).</summary>
    static class DelphiPatterns
    {
        // CRD metadata string (e.g. "CRD2-651-TMABDD11-639A4X-100kW-...")
        public static readonly Regex Crd =
            new Regex(@"(CRD\d?)-(\d{3})-([A-Z0-9]+)", RegexOptions.Compiled);

        // CRD extended: extract power rating (e.g. "100kW", "125kW")
        public static readonly Regex CrdPower =
            new Regex(@"CRD\d?-\d{3}-[A-Z0-9]+-[A-Z0-9]+-(\d{2,3})[kK][wW]", RegexOptions.Compiled);

        // Calibration delivery identifier (e.g. "SM05B006_DELIV_1 Apr 11 ...")
        public static readonly Regex Deliv =
            new Regex(@"([A-Z0-9]{4,15})_DELIV_\d", RegexOptions.Compiled);

        // Engine code from extended DELIV string (e.g. "T6C1HB05_DELIV_3_G9CD_ML6_...")
        public static readonly Regex DelivExt =
            new Regex(@"[A-Z0-9]+_DELIV_\d_([A-Z][A-Z0-9]{2,5})_", RegexOptions.Compiled);

        // Delphi internal HW part number (e.g. "28208056", "28292738")
        public static readonly Regex HwPart =
            new Regex(@"(?<!\d)(28[2-4]\d{5})(?!\d)", RegexOptions.Compiled);
    }

    /// <summary>Delco/ACDelco (GM) ECU patterns (E98, etc.).</summary>
    static class DelcoPatterns
    {
        // S-number hardware part (e.g. "S180161502A9")
        public static readonly Regex HwPart =
            new Regex(@"(S\d{9}[A-Z]\d)", RegexOptions.Compiled);

        // GM calibration number (e.g. "10214106AD") - 8 digits + 2 letters
        public static readonly Regex GmCal =
            new Regex(@"(?<![0-9])(\d{8}[A-Z]{2})(?![A-Z0-9])", RegexOptions.Compiled);
    }

    /// <summary>Transtron ECU patterns (Isuzu, Suzuki, etc.).</summary>
    static class TranstronPatterns
    {
        // Copyright string to confirm Transtron ECU
        public static readonly Regex Copyright =
            new Regex(@"Copyright Transtron", RegexOptions.Compiled);

        // Engine code + part number (e.g. "4JK1                z98250658")
        public static readonly Regex EnginePart =
            new Regex(@"([A-Z0-9]{3,6})\s{4,}z?(\d{7,8})", RegexOptions.Compiled);
    }

    /// <summary>BMW ECU patterns (DME/DDE identification).</summary>
    static class BmwPatterns
    {
        // BMW engine code from DME/DDE string (e.g. "#B47D20O0-F10" → "B47D20")
        public static readonly Regex Engine =
            new Regex(@"#([BNSM]\d{2}[A-Z]\d{2})[A-Z0-9]{0,2}-[EFGIU]\d{2}", RegexOptions.Compiled);
    }

    /// <summary>Mercedes ECU patterns (Delphi CRD files only).</summary>
    static class MercedesPatterns
    {
        // OEM part number (e.g. "A6511501879")
        public static readonly Regex PartNumber =
            new Regex(@"(?<![A-Z0-9])(A\d{10})(?!\d)", RegexOptions.Compiled);

        // A-number pair (e.g. "A6511501879  A0054469640")
        public static readonly Regex Pair =
            new Regex(@"(A\d{10})\s+(A\d{10})", RegexOptions.Compiled);
    }

    /// <summary>Generic patterns (VAG, PSA, engine info, fallback).</summary>
    static class GenericPatterns
    {
        // VAG OEM part numbers (e.g. "03L907309AE") with boundary assertions
        public static readonly Regex OemNumber =
            new Regex(@"(?<![0-9A-Za-z])(\d{2,3}[A-Z]\d{6}[A-Z]{0,3})(?![0-9A-Za-z])", RegexOptions.Compiled);

        // Engine type + engine code (e.g. "R4 2.0l TDI  CFFB")
        public static readonly Regex Engine =
            new Regex(@"([RVLI]\d\s+[\d.,]+[lL]\s+\w{2,6})\s+([A-Z]{3,4})", RegexOptions.Compiled);

        // Engine type without code (e.g. "R5 2,5L EDC")
        public static readonly Regex EngineType =
            new Regex(@"([RVLI]\d\s+[\d.,]+[lL]\s+[A-Z]{2,6})", RegexOptions.Compiled);

        // VAG engine code near controller address (e.g. "CAYCJ623" → CAYC)
        public static readonly Regex VagEngine =
            new Regex(@"([A-Z]{4})J623", RegexOptions.Compiled);

        // PSA part number in FOS calibration context
        public static readonly Regex PsaPart =
            new Regex(@"(?:FOS|PSAAPP|PSA_)[^\0]{0,80}(\d{10})", RegexOptions.Compiled);

        // OEM part number embedded in Bosch path extension (PSA/Iveco)
        public static readonly Regex BoschPathOem =
            new Regex(@"[A-Z0-9]{4,5}_[A-Z0-9]{4,5}_[A-Z0-9]{4,5}_(\d{10})//", RegexOptions.Compiled);

        // OEM SW number after .HEX calibration filename (Iveco/FPT)
        public static readonly Regex HexOem =
            new Regex(@"\.HEX\s+(\d{10})\s", RegexOptions.Compiled);
    }

    // ========================================================================
    // Flex tool filename constants
    // ========================================================================

    static class FlexConstants
    {
        public static readonly Dictionary<string, string> Brands = new Dictionary<string, string>
        {
            {"fomoco", "Ford"}, {"psa", "PSA"}, {"vag", "Volkswagen"},
            {"bmw", "BMW"}, {"fca", "FCA"}, {"renault", "Renault"},
            {"nissan", "Nissan"}, {"opel", "Opel"}, {"toyota", "Toyota"},
            {"hyundai", "Hyundai"}, {"kia", "Kia"}, {"mercedes", "Mercedes"},
            {"gm", "GM"}, {"jlr", "JLR"}, {"suzuki", "Suzuki"},
            {"subaru", "Subaru"}, {"mazda", "Mazda"}, {"mitsubishi", "Mitsubishi"},
            {"volvo", "Volvo"}, {"iveco", "Iveco"}, {"man", "MAN"},
            {"daf", "DAF"}, {"scania", "Scania"}, {"isuzu", "Isuzu"},
            {"honda", "Honda"}, {"jaguar", "Jaguar"}, {"landrover", "Land Rover"},
            {"porsche", "Porsche"}, {"audi", "Audi"}, {"seat", "Seat"},
            {"skoda", "Skoda"}, {"chrysler", "Chrysler"}, {"jeep", "Jeep"},
            {"dodge", "Dodge"}, {"alfa", "Alfa Romeo"}, {"fiat", "Fiat"},
            {"lancia", "Lancia"}, {"citroen", "Citroen"}, {"peugeot", "Peugeot"},
            {"ds", "DS"}, {"mini", "Mini"}, {"smart", "Smart"}
        };

        public static readonly HashSet<string> EcuBrands = new HashSet<string>
        {
            "bosch", "siemens", "delphi", "continental", "marelli",
            "denso", "delco", "valeo", "hitachi", "kefico", "transtron"
        };

        public static readonly HashSet<string> ReadMethods = new HashSet<string>
        {
            "obd", "bench", "boot"
        };
    }
}
