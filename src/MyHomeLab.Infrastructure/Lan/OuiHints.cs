namespace MyHomeLab.Infrastructure.Lan;

/// Curated MAC OUI (first 3 octets) → vendor display name. Best-effort identification
/// aid for the discovery list, not an authority: vendors make many device kinds, so
/// this table deliberately yields a name only — icon suggestions come from
/// DeviceTypeGuesser, which prefers SSDP model data and hostname keywords.
/// Prefixes are uppercase hex without separators ("AABBCC").
internal static class OuiHints
{
    private static readonly Dictionary<string, string> Vendors = new(StringComparer.OrdinalIgnoreCase)
    {
        // Phones / tablets / PCs (ambiguous kind — name only)
        ["3C22FB"] = "Apple",
        ["F0DC65"] = "Apple",
        ["8C7B9D"] = "Apple",
        ["001CB3"] = "Apple",
        ["8C8E76"] = "Samsung",
        ["E4E0A6"] = "Samsung",
        ["9C71F8"] = "Samsung",
        ["7825AD"] = "Xiaomi",
        ["7C49EB"] = "Xiaomi",
        ["00E04C"] = "Realtek",
        ["525400"] = "QEMU virtual NIC",
        ["080027"] = "VirtualBox virtual NIC",
        ["005056"] = "VMware virtual NIC",
        ["00155D"] = "Hyper-V virtual NIC",
        // Network gear
        ["C0C9E3"] = "Ubiquiti",
        ["802AA8"] = "Ubiquiti",
        ["E05A9F"] = "TP-Link",
        ["3C52A1"] = "TP-Link",
        ["A842A1"] = "Tenda",
        ["C83A35"] = "Tenda",
        ["0022B0"] = "D-Link",
        ["1C5FF2"] = "D-Link",
        ["000FB5"] = "Netgear",
        ["9C3DCF"] = "Netgear",
        ["001E2A"] = "Asus",
        ["2C56DC"] = "Asus",
        ["D0542D"] = "MikroTik",
        ["48A9A7"] = "MikroTik",
        ["E48E0C"] = "Fritz!Box (AVM)",
        ["3C37AB"] = "Fritz!Box (AVM)",
        // TV / media
        ["D0B214"] = "Roku",
        ["B8A37E"] = "Roku",
        ["FC5D19"] = "LG",
        ["A8851B"] = "LG",
        ["001E7A"] = "Sony",
        ["D05FB5"] = "Sony",
        ["F0B429"] = "Google (Nest/Chromecast)",
        ["3C5AB4"] = "Google (Nest/Chromecast)",
        ["44B951"] = "Amazon",
        ["0C47C9"] = "Amazon",
        ["7835A0"] = "Sonos",
        ["B8E937"] = "Sonos",
        // Consoles
        ["7C1E52"] = "Nintendo",
        ["CCFB65"] = "Nintendo",
        ["585E0C"] = "Microsoft (Xbox/Surface)",
        ["2818FD"] = "Microsoft (Xbox/Surface)",
        // IoT / maker boards
        ["24D7EB"] = "Espressif (ESP32/ESP8266)",
        ["30AEA4"] = "Espressif (ESP32/ESP8266)",
        ["B4E62D"] = "Espressif (ESP32/ESP8266)",
        ["DC4F22"] = "Espressif (ESP32/ESP8266)",
        ["E868E7"] = "Raspberry Pi",
        ["DCA632"] = "Raspberry Pi",
        ["E45F01"] = "Raspberry Pi",
        ["2CF432"] = "Espressif (ESP32/ESP8266)",
        ["349454"] = "Arduino",
        ["A4CF12"] = "Espressif (ESP32/ESP8266)",
        ["3842B5"] = "Shelly",
        ["3C6105"] = "Shelly",
        // Printers
        ["C4789F"] = "HP",
        ["3863BB"] = "HP",
        ["0017A4"] = "Brother",
        ["30055C"] = "Brother",
        ["00000C"] = "Cisco",
        ["F87B7A"] = "Canon",
        // Cameras / misc IoT
        ["D8A0E0"] = "Wyze",
        ["2CAA8E"] = "Wyze",
        ["E4AAEC"] = "Ring",
        ["685DEF"] = "Ring",
        ["18B430"] = "Nest Labs",
        ["F4F5DB"] = "Tuya IoT",
        ["A4432C"] = "Tuya IoT",
    };

    public static string? TryGetVendor(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac))
        {
            return null;
        }

        var hex = new string(mac.Where(char.IsAsciiHexDigit).ToArray());
        if (hex.Length < 6)
        {
            return null;
        }

        return Vendors.TryGetValue(hex[..6], out var vendor) ? vendor : null;
    }
}
