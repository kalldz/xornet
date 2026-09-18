using System.Collections.Concurrent;
using System.Timers;
using PacketDotNet;
using SharpPcap;
using Xornet.Data;
using Xornet.Utils;

namespace Xornet.Services;

public class BandwidthUpdatedEventArgs : EventArgs
{
    public IReadOnlyDictionary<string, (double DownKBps, double UpKBps)> Rates { get; }

    public BandwidthUpdatedEventArgs(IReadOnlyDictionary<string, (double, double)> rates)
    {
        Rates = rates;
    }
}

public class BandwidthService : IDisposable
{
    private readonly XornetConfig _config;
    private readonly ConcurrentDictionary<string, long> _bytesDown = new();
    private readonly ConcurrentDictionary<string, long> _bytesUp = new();
    private readonly ConcurrentDictionary<string, (double Down, double Up)> _currentRates = new();
    private System.Timers.Timer? _calculateTimer;

    public event EventHandler<BandwidthUpdatedEventArgs>? BandwidthUpdated;

    public BandwidthService()
    {
        _config = ConfigStore.Load();
    }

    public void Start()
    {
        if (_calculateTimer != null) return;

        _calculateTimer = new System.Timers.Timer(1000); // Calculate every second
        _calculateTimer.Elapsed += OnCalculateTimer;
        _calculateTimer.AutoReset = true;
        _calculateTimer.Start();
    }

    public void Stop()
    {
        _calculateTimer?.Stop();
        _calculateTimer?.Dispose();
        _calculateTimer = null;
    }

    public void OnPacketArrival(object sender, PacketCapture e)
    {
        var rawPacket = e.GetPacket();
        var packet = PacketDotNet.Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
        var ethPacket = packet.Extract<EthernetPacket>();
        if (ethPacket == null) return;

        var srcMac = MacAddressExtensions.FormatMac(ethPacket.SourceHardwareAddress.ToString());
        var dstMac = MacAddressExtensions.FormatMac(ethPacket.DestinationHardwareAddress.ToString());
        var length = (long)rawPacket.Data.Length;

        if (!string.IsNullOrEmpty(srcMac))
            _bytesUp.AddOrUpdate(srcMac, length, (_, old) => old + length);

        if (!string.IsNullOrEmpty(dstMac))
            _bytesDown.AddOrUpdate(dstMac, length, (_, old) => old + length);
    }

    public (double DownKBps, double UpKBps) GetRates(string mac)
    {
        var normalized = MacAddressExtensions.FormatMac(mac);
        if (_currentRates.TryGetValue(normalized, out var rates))
            return rates;
        return (0, 0);
    }

    public IReadOnlyDictionary<string, (double DownKBps, double UpKBps)> GetAllRates()
    {
        return _currentRates;
    }

    private void OnCalculateTimer(object? sender, ElapsedEventArgs e)
    {
        var allMacs = _bytesDown.Keys.Concat(_bytesUp.Keys)
            .Concat(_currentRates.Keys).Distinct().ToList();

        var snapshot = new Dictionary<string, (double, double)>();

        foreach (var mac in allMacs)
        {
            _bytesDown.TryRemove(mac, out var down);
            _bytesUp.TryRemove(mac, out var up);

            var downKBps = down / 1024.0;
            var upKBps = up / 1024.0;

            _currentRates[mac] = (downKBps, upKBps);
            snapshot[mac] = (downKBps, upKBps);
        }

        if (snapshot.Count > 0)
            BandwidthUpdated?.Invoke(this, new BandwidthUpdatedEventArgs(snapshot));
    }

    public void Dispose()
    {
        Stop();
    }
}
