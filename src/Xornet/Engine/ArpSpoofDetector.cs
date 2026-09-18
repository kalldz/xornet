using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using Xornet.Data;
using Xornet.Models;
using Xornet.Services;
using Xornet.Utils;

namespace Xornet.Engine;

public class AttackDetectedEventArgs : EventArgs
{
    public string AttackerMac { get; }
    public string? AttackerIp { get; }
    public DateTime DetectedAt { get; }

    public AttackDetectedEventArgs(string attackerMac, string? attackerIp)
    {
        AttackerMac = attackerMac;
        AttackerIp = attackerIp;
        DetectedAt = DateTime.UtcNow;
    }
}

public class ArpSpoofDetector : IDisposable
{
    private readonly Scanner _scanner;
    private readonly DeviceManager _deviceManager;
    private readonly XornetConfig _config;

    private LibPcapLiveDevice? _listenDevice;
    private LibPcapLiveDevice? _sendDevice;
    private CancellationTokenSource? _cts;
    private Task? _protectionTask;

    private readonly ConcurrentDictionary<string, DateTime> _attackerMacs = new();
    private readonly ConcurrentDictionary<string, bool> _protectedMacs = new();

    public bool IsRunning { get; private set; }
    public IReadOnlyDictionary<string, DateTime> AttackerMacs => _attackerMacs;

    public event EventHandler<AttackDetectedEventArgs>? AttackDetected;

    public ArpSpoofDetector(Scanner scanner, DeviceManager deviceManager)
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        _config = ConfigStore.Load();
    }

    public void Start()
    {
        if (IsRunning) return;

        _listenDevice = _deviceManager.CreateDevice("arp", 1000);
        _listenDevice.OnPacketArrival += OnPacketArrival;
        _listenDevice.StartCapture();

        _sendDevice = _deviceManager.CreateDevice("arp", 1000);

        IsRunning = true;
        _cts = new CancellationTokenSource();
        _protectionTask = Task.Run(ProtectionLoopAsync);
    }

    public void Stop()
    {
        IsRunning = false;
        _cts?.Cancel();
        _protectionTask?.Wait(TimeSpan.FromSeconds(2));
        _protectionTask?.Dispose();
        _protectionTask = null;

        if (_listenDevice != null)
        {
            try { _listenDevice.OnPacketArrival -= OnPacketArrival; } catch { }
            try { _listenDevice.StopCapture(); } catch { }
            try { _listenDevice.Close(); } catch { }
            try { _listenDevice.Dispose(); } catch { }
            _listenDevice = null;
        }

        if (_sendDevice != null)
        {
            try { _sendDevice.Close(); } catch { }
            try { _sendDevice.Dispose(); } catch { }
            _sendDevice = null;
        }

        _cts?.Dispose();
        _cts = null;
        _attackerMacs.Clear();
    }

    public void ProtectDevice(string macAddress)
    {
        var normalized = MacAddressExtensions.FormatMac(macAddress);
        if (!string.IsNullOrEmpty(normalized))
            _protectedMacs.TryAdd(normalized, true);
    }

    public void UnprotectDevice(string macAddress)
    {
        var normalized = MacAddressExtensions.FormatMac(macAddress);
        _protectedMacs.TryRemove(normalized, out _);
    }

    public bool IsProtected(string macAddress)
    {
        var normalized = MacAddressExtensions.FormatMac(macAddress);
        return !string.IsNullOrEmpty(normalized) && _protectedMacs.ContainsKey(normalized);
    }

    public IReadOnlyCollection<string> GetProtectedMacs() => _protectedMacs.Keys.ToList();

    private void OnPacketArrival(object sender, PacketCapture e)
    {
        var raw = e.GetPacket();
        var packet = PacketDotNet.Packet.ParsePacket(raw.LinkLayerType, raw.Data);
        var arp = packet.Extract<ArpPacket>();
        if (arp == null) return;

        // Detect gateway impersonation
        if (HostInfo.GatewayIp != null &&
            HostInfo.GatewayMac != null &&
            HostInfo.HostMac != null &&
            arp.SenderProtocolAddress != null &&
            arp.SenderHardwareAddress != null &&
            arp.SenderProtocolAddress.Equals(HostInfo.GatewayIp) &&
            !arp.SenderHardwareAddress.Equals(HostInfo.GatewayMac) &&
            !arp.SenderHardwareAddress.Equals(HostInfo.HostMac))
        {
            var attackerMac = MacAddressExtensions.FormatMac(arp.SenderHardwareAddress.ToString());
            if (string.IsNullOrEmpty(attackerMac)) return;

            var isNew = !_attackerMacs.ContainsKey(attackerMac);
            _attackerMacs[attackerMac] = DateTime.UtcNow;

            if (isNew)
            {
                var attackerIp = ResolveIpFromMac(attackerMac);
                AttackDetected?.Invoke(this, new AttackDetectedEventArgs(attackerMac, attackerIp));
            }
        }
    }

    private async Task ProtectionLoopAsync()
    {
        while (_cts != null && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                if (_protectedMacs.Count > 0)
                    SendCorrectiveArp();

                await Task.Delay(_config.ProtectIntervalMs, _cts.Token);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private void SendCorrectiveArp()
    {
        if (HostInfo.GatewayIp == null || HostInfo.GatewayMac == null || HostInfo.HostMac == null || _sendDevice == null)
            return;

        var clients = _scanner.GetClients();

        foreach (var mac in _protectedMacs.Keys)
        {
            if (mac.Equals(HostInfo.HostMac.ToString(), StringComparison.OrdinalIgnoreCase))
                continue;

            if (!clients.TryGetValue(mac, out var target)) continue;
            if (!target.IsOnline) continue;

            // Send corrective ARP: "Gateway IP = real GatewayMac"
            var arpReply = new ArpPacket(
                ArpOperation.Response,
                targetHardwareAddress: target.Mac,
                targetProtocolAddress: target.Ip,
                senderHardwareAddress: HostInfo.GatewayMac,
                senderProtocolAddress: HostInfo.GatewayIp);

            var ethPacket = new EthernetPacket(
                sourceHardwareAddress: HostInfo.GatewayMac,
                destinationHardwareAddress: target.Mac,
                EthernetType.Arp)
            {
                PayloadPacket = arpReply
            };

            _sendDevice.SendPacket(ethPacket.Bytes);
        }
    }

    private string? ResolveIpFromMac(string macAddress)
    {
        var clients = _scanner.GetClients();
        if (clients.TryGetValue(macAddress, out var client))
            return client.Ip.ToString();
        return null;
    }

    public void Dispose()
    {
        Stop();
    }
}
