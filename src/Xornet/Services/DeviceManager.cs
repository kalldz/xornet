using System.Net.NetworkInformation;
using SharpPcap;
using SharpPcap.LibPcap;
using Xornet.Data;

namespace Xornet.Services;

public interface IPacketCapture
{
    void StartCapture();
    void StopCapture();
}

public class DeviceManager : IDisposable
{
    private LibPcapLiveDevice? _device;
    private readonly List<LibPcapLiveDevice> _allDevices;

    public DeviceManager()
    {
        _allDevices = CaptureDeviceList.Instance
            .OfType<LibPcapLiveDevice>()
            .Where(d => d.Addresses.Any(a => a.Addr.type == Sockaddr.AddressTypes.HARDWARE))
            .ToList();

        if (_allDevices.Count == 0)
            throw new NotSupportedException("No supported network adapter found. Please install Npcap (Windows) or libpcap (Linux).");

        var savedDevice = DataStore.LoadDeviceName();
        if (!string.IsNullOrEmpty(savedDevice))
        {
            _device = _allDevices.FirstOrDefault(d => d.Name == savedDevice);
        }

        _device ??= _allDevices.FirstOrDefault(d => d.Interface.GatewayAddresses.Count > 0)
                 ?? _allDevices.First();

        HostInfo.SetHostInfo(_device);
    }

    public LibPcapLiveDevice Device => _device ?? throw new InvalidOperationException("No device selected");

    public IReadOnlyList<LibPcapLiveDevice> GetAllDevices() => _allDevices;

    public LibPcapLiveDevice CreateDevice(string filter = "arp", int timeout = 1000)
    {
        var device = new LibPcapLiveDevice(_device!.Interface);
        device.Open(DeviceModes.Promiscuous, timeout);

        if (!string.IsNullOrEmpty(filter))
            device.Filter = filter;

        return device;
    }

    public void ChangeDevice(string deviceName)
    {
        var newDevice = _allDevices.FirstOrDefault(d => d.Name == deviceName);
        if (newDevice is null)
            throw new ArgumentException($"Device '{deviceName}' not found", nameof(deviceName));

        _device = newDevice;
        HostInfo.Clear();
        HostInfo.SetHostInfo(newDevice);
        DataStore.SaveDeviceName(deviceName);
    }

    public void Dispose()
    {
        foreach (var dev in _allDevices)
        {
            try { dev.Close(); } catch { /* ignore */ }
            try { dev.Dispose(); } catch { /* ignore */ }
        }
    }
}
