using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Timers;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using Xornet.Data;
using Xornet.Models;
using Xornet.Services;
using Xornet.Utils;

namespace Xornet.Engine;

public class Scanner : IDisposable
{
    private readonly DeviceManager _deviceManager;
    private readonly XornetConfig _config;
    private readonly NameResolver _nameResolver;
    private readonly ConcurrentDictionary<string, Client> _clients = new();
    private readonly HashSet<string> _processingIps = new();
    private readonly object _processingLock = new();

    private LibPcapLiveDevice? _device;
    private System.Timers.Timer? _backgroundScanTimer;
    private System.Timers.Timer? _isAliveTimer;
    private bool _isScanning;

    public event EventHandler? ClientsChanged;

    public Scanner(DeviceManager deviceManager, NameResolver? nameResolver = null)
    {
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        _config = ConfigStore.Load();
        _nameResolver = nameResolver ?? new NameResolver();
        LoadSavedClients();
    }

    public IReadOnlyDictionary<string, Client> GetClients() => _clients;

    public void Start()
    {
        if (_isScanning) return;
        _isScanning = true;

        _device = _deviceManager.CreateDevice("arp", 1000);
        _device.OnPacketArrival += OnPacketArrival;
        _device.StartCapture();

        _ = ScanAsync();

        _backgroundScanTimer = new System.Timers.Timer(_config.ArpProbeIntervalMs);
        _backgroundScanTimer.Elapsed += async (_, _) => await ScanAsync();
        _backgroundScanTimer.AutoReset = true;
        _backgroundScanTimer.Start();

        _isAliveTimer = new System.Timers.Timer(_config.OfflineTimeoutSecs * 1000);
        _isAliveTimer.Elapsed += OnIsAliveTimedEvent;
        _isAliveTimer.AutoReset = true;
        _isAliveTimer.Start();
    }

    public void Stop()
    {
        _isScanning = false;
        _backgroundScanTimer?.Stop();
        _backgroundScanTimer?.Dispose();
        _isAliveTimer?.Stop();
        _isAliveTimer?.Dispose();

        if (_device != null)
        {
            try { _device.StopCapture(); } catch { }
            try { _device.Close(); } catch { }
            try { _device.Dispose(); } catch { }
            _device = null;
        }
    }

    public async Task ScanAsync()
    {
        if (!_isScanning || _device == null) return;

        var targets = BuildProbeList();
        if (targets.Count == 0) return;

        lock (_processingLock)
        {
            _processingIps.Clear();
            foreach (var ip in targets)
                _processingIps.Add(ip.ToString());
        }

        // Send ARP probes in batches
        const int batchSize = 50;
        for (int i = 0; i < targets.Count; i += batchSize)
        {
            var batch = targets.Skip(i).Take(batchSize).ToList();
            foreach (var ip in batch)
            {
                try { SendArpProbe(ip); }
                catch { /* ignore send errors */ }
            }
            await Task.Delay(10);
        }

        // Wait a bit for ARP replies, then do ICMP fallback
        await Task.Delay(2000);
        await PingFallbackAsync(targets);

        SaveClients();
    }

    private List<IPAddress> BuildProbeList()
    {
        var host = HostInfo.HostIp;
        var mask = HostInfo.NetMask;

        if (host == null || mask == null)
        {
            // Fallback to /24
            var root = HostInfo.GetRootIp();
            var fallback = new List<IPAddress>(254);
            for (int i = 1; i <= 254; i++)
                fallback.Add(IPAddress.Parse(root + i));
            return fallback;
        }

        var hostBytes = host.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();

        var netBytes = new byte[4];
        var bcastBytes = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            netBytes[i] = (byte)(hostBytes[i] & maskBytes[i]);
            bcastBytes[i] = (byte)(hostBytes[i] | ~maskBytes[i]);
        }

        uint netInt = IpAddressExtensions.ToUint(new IPAddress(netBytes));
        uint bcastInt = IpAddressExtensions.ToUint(new IPAddress(bcastBytes));
        uint hostCount = bcastInt - netInt - 1;

        // Safety: limit to /16 (65534 hosts)
        if (hostCount > 65534)
        {
            var root = host.GetRootIp();
            var fallback = new List<IPAddress>(254);
            for (int i = 1; i <= 254; i++)
                fallback.Add(IPAddress.Parse(root + i));
            return fallback;
        }

        var result = new List<IPAddress>((int)hostCount);
        for (uint i = 1; i <= hostCount; i++)
        {
            result.Add(IpAddressExtensions.FromUint(netInt + i));
        }
        return result;
    }

    private void SendArpProbe(IPAddress targetIp)
    {
        if (HostInfo.HostMac == null || HostInfo.HostIp == null || _device == null)
            return;

        var arpPacket = new ArpPacket(ArpOperation.Request,
            targetHardwareAddress: HostInfo.EmptyMac,
            targetProtocolAddress: targetIp,
            senderHardwareAddress: HostInfo.HostMac,
            senderProtocolAddress: HostInfo.HostIp);

        var ethernetPacket = new EthernetPacket(
            sourceHardwareAddress: HostInfo.HostMac,
            destinationHardwareAddress: HostInfo.BroadcastMac,
            EthernetType.Arp)
        {
            PayloadPacket = arpPacket
        };

        _device.SendPacket(ethernetPacket.Bytes);
    }

    private void OnPacketArrival(object sender, PacketCapture packetCapture)
    {
        var rawPacket = packetCapture.GetPacket();
        var packet = PacketDotNet.Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
        var arpPacket = packet.Extract<ArpPacket>();
        if (arpPacket == null || arpPacket.Operation == ArpOperation.Request)
            return;

        var mac = MacAddressExtensions.FormatMac(arpPacket.SenderHardwareAddress.ToString());
        if (string.IsNullOrEmpty(mac)) return;

        lock (_processingLock)
        {
            _processingIps.Remove(arpPacket.SenderProtocolAddress?.ToString() ?? "");
        }

        var senderIp = arpPacket.SenderProtocolAddress;
        var senderMac = arpPacket.SenderHardwareAddress;
        if (senderIp == null || senderMac == null)
            return;

        if (!_clients.ContainsKey(mac))
        {
            var client = new Client(senderIp, senderMac);
            if (_clients.TryAdd(mac, client))
            {
                _nameResolver.ResolveVendorName(client);
                _nameResolver.ResolveClientName(client);
                ClientsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        else
        {
            _clients[mac].UpdateLastArpTime();
            if (!_clients[mac].IsOnline)
            {
                _clients[mac].IsOnline = true;
                ClientsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private async Task PingFallbackAsync(List<IPAddress> targets)
    {
        var knownIps = new HashSet<string>(_clients.Values.Select(c => c.Ip.ToString()));

        List<IPAddress> toProbe;
        lock (_processingLock)
        {
            toProbe = targets
                .Where(ip => !knownIps.Contains(ip.ToString()) && _processingIps.Contains(ip.ToString()))
                .ToList();
        }

        if (toProbe.Count == 0) return;

        using var sem = new SemaphoreSlim(_config.MaxConcurrency);

        var tasks = toProbe.Select(async ip =>
        {
            await sem.WaitAsync();
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, _config.PingTimeoutMs);
                if (reply.Status != IPStatus.Success) return null;

                var syntheticKey = $"ICMP_{ip}";
                var client = new Client(ip, PhysicalAddress.None) { Type = Models.Enums.ClientType.Icmp };
                if (_clients.TryAdd(syntheticKey, client))
                {
                    _nameResolver.ResolveClientName(client);
                    return client;
                }
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                sem.Release();
            }
        });

        var added = (await Task.WhenAll(tasks)).Where(c => c != null).ToList();
        if (added.Count > 0)
        {
            ClientsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnIsAliveTimedEvent(object? source, ElapsedEventArgs e)
    {
        bool statusChanged = false;
        foreach (var kvp in _clients)
        {
            var client = kvp.Value;
            if (!client.IsGateway() &&
                !client.IsLocalDevice() &&
                client.Type != Models.Enums.ClientType.Icmp &&
                (DateTime.UtcNow - client.LastArpTime).TotalSeconds > _config.OfflineTimeoutSecs)
            {
                if (client.IsOnline)
                {
                    client.IsOnline = false;
                    statusChanged = true;
                }
            }
        }

        if (statusChanged)
            ClientsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LoadSavedClients()
    {
        var saved = DataStore.LoadClients();
        foreach (var s in saved)
        {
            try
            {
                var client = s.ToClient();
                var key = client.GetMacString();
                if (!string.IsNullOrEmpty(key) && !key.Equals("000000000000"))
                {
                    _clients.TryAdd(key, client);
                }
            }
            catch { /* skip invalid entries */ }
        }
    }

    private void SaveClients()
    {
        var serialized = _clients.Values.Select(SerializedClient.FromClient).ToList();
        DataStore.SaveClients(serialized);
    }

    public void Dispose()
    {
        Stop();
        SaveClients();
    }
}
