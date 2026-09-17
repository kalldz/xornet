using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SharpPcap;
using SharpPcap.LibPcap;

namespace Xornet.Services;

public static class HostInfo
{
    public static IPAddress? HostIp { get; private set; }
    public static PhysicalAddress? HostMac { get; private set; }
    public static IPAddress? GatewayIp { get; private set; }
    public static PhysicalAddress? GatewayMac { get; private set; }
    public static string? NetworkAdapterName { get; private set; }
    public static IPAddress? NetMask { get; private set; }

    public static readonly PhysicalAddress BroadcastMac = PhysicalAddress.Parse("FFFFFFFFFFFF");
    public static readonly PhysicalAddress EmptyMac = PhysicalAddress.Parse("000000000000");

    public static void SetHostInfo(LibPcapLiveDevice device)
    {
        foreach (var address in device.Addresses)
        {
            if (address.Addr.type == Sockaddr.AddressTypes.AF_INET_AF_INET6 &&
                address.Addr.ipAddress?.AddressFamily == AddressFamily.InterNetwork)
            {
                HostIp = address.Addr.ipAddress;
                NetMask = address.Netmask?.ipAddress;
                break;
            }
        }

        foreach (var address in device.Addresses)
        {
            if (address.Addr.type == Sockaddr.AddressTypes.HARDWARE)
            {
                HostMac = address.Addr.hardwareAddress;
                break;
            }
        }

        NetworkAdapterName = device.Interface.FriendlyName;
        GatewayIp = device.Interface.GatewayAddresses.Count > 0
            ? device.Interface.GatewayAddresses[0]
            : null;

        if (GatewayIp is not null && HostIp is not null && HostMac is not null)
        {
            try
            {
                var arp = new ARP(device);
                var resolved = arp.Resolve(GatewayIp, HostIp, HostMac);
                GatewayMac = resolved;
            }
            catch
            {
                GatewayMac = null;
            }
        }
    }

    public static string GetRootIp()
    {
        if (HostIp is null)
            return "192.168.1.";

        var parts = HostIp.ToString().Split('.');
        if (parts.Length == 4)
            return $"{parts[0]}.{parts[1]}.{parts[2]}.";

        return "192.168.1.";
    }

    public static void Clear()
    {
        HostIp = null;
        HostMac = null;
        GatewayIp = null;
        GatewayMac = null;
        NetworkAdapterName = null;
        NetMask = null;
    }
}
