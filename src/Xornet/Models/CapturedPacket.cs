using System.Net;
using System.Net.NetworkInformation;
using Xornet.Utils;

namespace Xornet.Models;

public enum PacketProtocol
{
    Unknown,
    Arp,
    Icmp,
    Tcp,
    Udp,
    Dns,
    Http,
    Https,
    Tls
}

public class CapturedPacket
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public PhysicalAddress SourceMac { get; set; } = PhysicalAddress.None;
    public PhysicalAddress DestinationMac { get; set; } = PhysicalAddress.None;
    public IPAddress? SourceIp { get; set; }
    public IPAddress? DestinationIp { get; set; }
    public int SourcePort { get; set; }
    public int DestinationPort { get; set; }
    public PacketProtocol Protocol { get; set; } = PacketProtocol.Unknown;
    public int Length { get; set; }
    public string Info { get; set; } = string.Empty;

    public string SourceMacString => MacAddressExtensions.FormatMac(SourceMac.ToString());
    public string DestinationMacString => MacAddressExtensions.FormatMac(DestinationMac.ToString());

    public override string ToString()
    {
        var src = SourceIp?.ToString() ?? SourceMacString;
        var dst = DestinationIp?.ToString() ?? DestinationMacString;
        var ports = SourcePort > 0 ? $":{SourcePort}->{DestinationPort}" : "";
        return $"[{Timestamp:HH:mm:ss.fff}] {Protocol,-8} {src,-20} -> {dst,-20} {ports,-12} {Length,6} bytes {Info}";
    }
}
