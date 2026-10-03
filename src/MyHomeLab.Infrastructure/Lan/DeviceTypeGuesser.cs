namespace MyHomeLab.Infrastructure.Lan;

/// Best-effort device typing for discovered unknowns. Precedence is deliberate:
/// SSDP model data (the device describing itself) beats hostname keywords, which beat
/// the OUI vendor name. The icon is only suggested when the signal is unambiguous
/// (TV model strings, console names, phone hostnames); the OUI table contributes a
/// vendor display name only, because vendors make many device kinds.
internal static class DeviceTypeGuesser
{
    public static (string? DeviceType, string? SuggestedIcon) Guess(
        string? ssdpName, string? ssdpModel, string? ouiVendor, string? hostname)
    {
        var evidence = $"{ssdpName} {ssdpModel}".ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(evidence.Trim()))
        {
            if (ContainsAny(evidence, "tv", "bravia", "roku", "fire tv", "apple tv", "shield", "chromecast", "smartcast", "webos", "tizen"))
            {
                return (Clean(ssdpModel ?? ssdpName), "tv");
            }

            if (ContainsAny(evidence, "xbox", "playstation", "nintendo", "wii"))
            {
                return (Clean(ssdpModel ?? ssdpName), "sports_esports");
            }

            if (ContainsAny(evidence, "printer", "print"))
            {
                return (Clean(ssdpModel ?? ssdpName), "printer");
            }

            if (ContainsAny(evidence, "nas", "synology", "diskstation", "qnap"))
            {
                return (Clean(ssdpModel ?? ssdpName), "dns");
            }

            if (!string.IsNullOrWhiteSpace(ssdpModel) || !string.IsNullOrWhiteSpace(ssdpName))
            {
                return (Clean(ssdpModel ?? ssdpName), null);
            }
        }

        var host = (hostname ?? string.Empty).ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(host))
        {
            if (ContainsAny(host, "iphone", "ipad", "android", "galaxy", "pixel", "oneplus", "mobile", "phone", "redmi", "poco"))
            {
                return (Clean(hostname), "smartphone");
            }

            if (ContainsAny(host, "tv", "bravia", "roku", "firetv", "chromecast", "kodi"))
            {
                return (Clean(hostname), "tv");
            }

            if (ContainsAny(host, "xbox", "playstation", "ps5", "ps4", "nintendo"))
            {
                return (Clean(hostname), "sports_esports");
            }

            if (ContainsAny(host, "printer", "print", "laserjet", "officejet"))
            {
                return (Clean(hostname), "printer");
            }

            if (ContainsAny(host, "raspberry", "raspberrypi"))
            {
                return (Clean(hostname), "computer");
            }

            if (ContainsAny(host, "nas", "synology", "qnap"))
            {
                return (Clean(hostname), "dns");
            }

            if (ContainsAny(host, "esp", "tasmota", "shelly", "esphome"))
            {
                return (Clean(hostname), null);
            }
        }

        if (!string.IsNullOrWhiteSpace(ouiVendor))
        {
            var vendor = ouiVendor.ToLowerInvariant();
            if (vendor.Contains("raspberry"))
            {
                return (ouiVendor, "computer");
            }

            if (vendor.Contains("roku"))
            {
                return (ouiVendor, "tv");
            }

            return (ouiVendor, null);
        }

        return (null, null);
    }

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(needle => haystack.Contains(needle, StringComparison.Ordinal));

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
