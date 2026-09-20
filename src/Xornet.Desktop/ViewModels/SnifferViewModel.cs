using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Xornet.Models;
using Xornet.Services;

namespace Xornet.Desktop.ViewModels;

public partial class SnifferViewModel : ObservableObject
{
    private readonly SnifferService _sniffer;

    [ObservableProperty]
    private ObservableCollection<CapturedPacket> _packets = new();

    [ObservableProperty]
    private CapturedPacket? _selectedPacket;

    [ObservableProperty]
    private bool _isCapturing;

    [ObservableProperty]
    private string _statusText = "Stopped";

    [ObservableProperty]
    private int _packetCount;

    [ObservableProperty]
    private string _filterText = string.Empty;

    public SnifferViewModel(SnifferService sniffer)
    {
        _sniffer = sniffer;
        _sniffer.PacketsFlushed += OnPacketsFlushed;
    }

    partial void OnFilterTextChanged(string value)
    {
        // Filtering applied in view via CollectionView
    }

    private void OnPacketsFlushed(object? sender, List<CapturedPacket> packets)
    {
        foreach (var packet in packets)
        {
            Packets.Add(packet);
        }
        PacketCount = Packets.Count;
    }

    [RelayCommand]
    private void ToggleCapture()
    {
        if (IsCapturing)
        {
            _sniffer.StopCapture();
            IsCapturing = false;
            StatusText = "Stopped";
        }
        else
        {
            Packets.Clear();
            _sniffer.StartCapture();
            IsCapturing = true;
            StatusText = "Capturing...";
        }
    }

    [RelayCommand]
    private void Clear()
    {
        Packets.Clear();
        PacketCount = 0;
    }
}
