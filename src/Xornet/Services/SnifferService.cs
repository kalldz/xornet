using System.Collections.Concurrent;
using PacketDotNet;
using SharpPcap;
using Xornet.Data;
using Xornet.Engine;
using Xornet.Models;
using Xornet.Utils;

namespace Xornet.Services;

public class SnifferService : IDisposable
{
    private readonly Scanner _scanner;
    private readonly XornetConfig _config;
    private readonly ConcurrentQueue<CapturedPacket> _pending = new();
    private System.Threading.Timer? _flushTimer;
    private bool _isCapturing;

    public event EventHandler<List<CapturedPacket>>? PacketsFlushed;

    public SnifferService(Scanner scanner)
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        _config = ConfigStore.Load();
    }

    public void StartCapture()
    {
        if (_isCapturing) return;
        _isCapturing = true;

        // Tap into scanner's device - no new pcap handle needed
        _scanner.AddPacketHandler(OnPacketArrival);

        _flushTimer = new System.Threading.Timer(_ => Flush(), null,
            _config.FlushIntervalMs, _config.FlushIntervalMs);
    }

    public void StopCapture()
    {
        _isCapturing = false;
        _flushTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _flushTimer?.Dispose();
        _flushTimer = null;
        _pending.Clear();
    }

    private void OnPacketArrival(object? sender, PacketCapture e)
    {
        var decoded = Decode(e);
        if (decoded != null)
            _pending.Enqueue(decoded);
    }

    private void Flush()
    {
        var batch = new List<CapturedPacket>();
        while (_pending.TryDequeue(out var packet))
        {
            batch.Add(packet);
        }

        if (batch.Count > 0)
            PacketsFlushed?.Invoke(this, batch);
    }

    private CapturedPacket? Decode(PacketCapture e)
    {
        var raw = e.GetPacket();
        var packet = PacketDotNet.Packet.ParsePacket(raw.LinkLayerType, raw.Data);
        var eth = packet.Extract<EthernetPacket>();
        if (eth == null) return null;

        var result = new CapturedPacket
        {
            Length = raw.Data.Length,
            SourceMac = eth.SourceHardwareAddress,
            DestinationMac = eth.DestinationHardwareAddress
        };

        // ARP
        var arp = packet.Extract<ArpPacket>();
        if (arp != null)
        {
            result.Protocol = PacketProtocol.Arp;
            result.SourceIp = arp.SenderProtocolAddress;
            result.DestinationIp = arp.TargetProtocolAddress;
            result.Info = $"{arp.Operation}";
            return result;
        }

        // IPv4
        var ip = packet.Extract<IPv4Packet>();
        if (ip != null)
        {
            result.SourceIp = ip.SourceAddress;
            result.DestinationIp = ip.DestinationAddress;

            // ICMP
            var icmp = packet.Extract<IcmpV4Packet>();
            if (icmp != null)
            {
                result.Protocol = PacketProtocol.Icmp;
                result.Info = $"Type={icmp.TypeCode}";
                return result;
            }

            // TCP
            var tcp = packet.Extract<TcpPacket>();
            if (tcp != null)
            {
                result.SourcePort = tcp.SourcePort;
                result.DestinationPort = tcp.DestinationPort;
                result.Info = $"Flags={tcp.Flags}";

                // Detect protocol by port
                result.Protocol = DetectProtocolByPort(tcp.SourcePort, tcp.DestinationPort);
                return result;
            }

            // UDP
            var udp = packet.Extract<UdpPacket>();
            if (udp != null)
            {
                result.SourcePort = udp.SourcePort;
                result.DestinationPort = udp.DestinationPort;
                result.Protocol = PacketProtocol.Udp;

                // DNS detection (port 53)
                if (udp.SourcePort == 53 || udp.DestinationPort == 53)
                    result.Protocol = PacketProtocol.Dns;

                return result;
            }
        }

        return result;
    }

    private static PacketProtocol DetectProtocolByPort(int srcPort, int dstPort)
    {
        var port = dstPort > 0 ? dstPort : srcPort;
        return port switch
        {
            80 => PacketProtocol.Http,
            443 => PacketProtocol.Https,
            53 => PacketProtocol.Dns,
            _ => PacketProtocol.Tcp
        };
    }

    public void Dispose()
    {
        StopCapture();
    }
}
