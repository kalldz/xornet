using System.Net.NetworkInformation;

namespace Xornet.Utils;

public static class MacAddressExtensions
{
    public static string FormatMac(string mac)
    {
        if (string.IsNullOrEmpty(mac))
            return string.Empty;

        var clean = mac.Replace(":", "").Replace("-", "").Replace(".", "").ToUpperInvariant();
        if (clean.Length != 12)
            return mac.ToUpperInvariant();

        return clean;
    }

    public static string FormatWithSeparator(this PhysicalAddress mac)
    {
        var clean = FormatMac(mac.ToString());
        if (clean.Length != 12)
            return mac.ToString().ToUpperInvariant();

        return string.Join(":", Enumerable.Range(0, 6).Select(i => clean.Substring(i * 2, 2)));
    }

    public static string GetOui(this PhysicalAddress mac)
    {
        var macStr = FormatMac(mac.ToString());
        if (macStr.Length < 6)
            return string.Empty;
        return macStr.Substring(0, 6);
    }
}
