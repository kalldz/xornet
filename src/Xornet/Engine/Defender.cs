using System.Net.NetworkInformation;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using Xornet.Data;
using Xornet.Services;
using Xornet.Utils;

namespace Xornet.Engine;

public class Defender : IDisposable
{
    private readonly DeviceManager _deviceManager;
    private readonly XornetConfig _config;

    private LibPcapLiveDevice? _device;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _defenseTask;

    public bool IsDefending { get; private set; }
    public bool SpoofingDetected { get; private set; }

    public event EventHandler? SpoofingDetectedChanged;

    public Defender(DeviceManager deviceManager)
    {
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        _config = ConfigStore.Load();
    }

    public void Defend()
    {
        if (IsDefending) return;

        _device = _deviceManager.CreateDevice("arp", 1000);
        _device.OnPacketArrival += OnPacketArrival;
        _device.StartCapture();

        IsDefending = true;
        SpoofingDetected = false;
        _cancellationTokenSource = new CancellationTokenSource();

        _defenseTask = Task.Run(async () =>
        {
            while (IsDefending && !(_cancellationTokenSource?.Token.IsCancellationRequested ?? false))
            {
                try
                {
                    FixTarget();
                    await Task.Delay(_config.DefenseIntervalMs, _cancellationTokenSource?.Token ?? CancellationToken.None);
                }
                catch (OperationCanceledException) { break; }
            }
        });
    }

    public void Stop()
    {
        IsDefending = false;
        _cancellationTokenSource?.Cancel();
        _defenseTask?.Wait(TimeSpan.FromSeconds(2));
        _defenseTask?.Dispose();
        _defenseTask = null;

        if (_device != null)
        {
            try { _device.OnPacketArrival -= OnPacketArrival; } catch { }
            try { _device.StopCapture(); } catch { }
            try { _device.Close(); } catch { }
            try { _device.Dispose(); } catch { }
            _device = null;
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        SpoofingDetected = false;
    }

    private void OnPacketArrival(object sender, PacketCapture packetCapture)
    {
        var rawPacket = packetCapture.GetPacket();
        var packet = PacketDotNet.Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
        var arpPacket = packet.Extract<ArpPacket>();
        if (arpPacket == null) return;

        // Detect: ARP packet claiming gateway IP but with different MAC
        if (arpPacket.SenderProtocolAddress != null &&
            HostInfo.GatewayIp != null &&
            arpPacket.SenderProtocolAddress.Equals(HostInfo.GatewayIp) &&
            arpPacket.SenderHardwareAddress != null &&
            HostInfo.GatewayMac != null &&
            !arpPacket.SenderHardwareAddress.Equals(HostInfo.GatewayMac))
        {
            if (!SpoofingDetected)
            {
                SpoofingDetected = true;
                SpoofingDetectedChanged?.Invoke(this, EventArgs.Empty);
            }
            FixTarget();
        }
    }

    private void FixTarget()
    {
        if (HostInfo.GatewayMac == null || HostInfo.GatewayIp == null || HostInfo.HostMac == null || _device == null)
            return;

        var arpPacket = new ArpPacket(ArpOperation.Request,
            targetHardwareAddress: HostInfo.GatewayMac,
            targetProtocolAddress: HostInfo.GatewayIp,
            senderHardwareAddress: HostInfo.HostMac,
            senderProtocolAddress: HostInfo.GatewayIp);

        var etherPacket = new EthernetPacket(
            sourceHardwareAddress: HostInfo.HostMac,
            destinationHardwareAddress: HostInfo.GatewayMac,
            EthernetType.Arp)
        {
            PayloadPacket = arpPacket
        };

        _device.SendPacket(etherPacket.Bytes);
    }

    public void Dispose()
    {
        Stop();
    }
}
