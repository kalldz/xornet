using System.Net;
using System.Net.NetworkInformation;
using Xornet.Models.Enums;
using Xornet.Services;
using Xornet.Utils;

namespace Xornet.Models;

public class Client
{
    public IPAddress Ip { get; }
    public PhysicalAddress Mac { get; }
    public string Name { get; set; } = "Unknown";
    public string Vendor { get; set; } = "NA";
    public ClientType Type { get; set; } = ClientType.Arp;
    public bool IsOnline { get; set; } = true;
    public bool IsKilled { get; set; } = false;
    public DateTime LastArpTime { get; private set; } = DateTime.UtcNow;
    public DateTime FirstSeen { get; } = DateTime.UtcNow;

    public Client(IPAddress ip, PhysicalAddress mac)
    {
        Ip = ip ?? throw new ArgumentNullException(nameof(ip));
        Mac = mac ?? throw new ArgumentNullException(nameof(mac));
    }

    public void UpdateLastArpTime()
    {
        LastArpTime = DateTime.UtcNow;
    }

    public bool IsGateway()
    {
        return HostInfo.GatewayIp is not null && Ip.Equals(HostInfo.GatewayIp);
    }

    public bool IsLocalDevice()
    {
        return HostInfo.HostMac is not null && Mac.Equals(HostInfo.HostMac);
    }

    public string GetMacString()
    {
        return Mac.ToString().ToUpperInvariant();
    }

    public string GetFormattedMacString()
    {
        return Mac.FormatWithSeparator();
    }

    public string GetOui()
    {
        var macStr = GetMacString();
        if (macStr.Length < 6)
            return string.Empty;
        return macStr.Substring(0, 6);
    }

    public override string ToString()
    {
        return $"{Ip} [{GetMacString()}] {Name} ({Vendor})";
    }
}
