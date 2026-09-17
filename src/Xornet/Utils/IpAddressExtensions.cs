using System.Net;

namespace Xornet.Utils;

public static class IpAddressExtensions
{
    public static string GetRootIp(this IPAddress ip)
    {
        var parts = ip.ToString().Split('.');
        if (parts.Length == 4)
            return $"{parts[0]}.{parts[1]}.{parts[2]}.";
        return "192.168.1.";
    }

    public static uint ToUint(this IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    public static IPAddress FromUint(uint value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return new IPAddress(bytes);
    }
}
