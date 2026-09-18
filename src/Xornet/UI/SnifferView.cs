using Terminal.Gui;
using Xornet.Models;
using Xornet.Services;

namespace Xornet.UI;

public class SnifferView : View
{
    private readonly SnifferService _sniffer;
    private readonly ListView _packetListView;
    private readonly List<CapturedPacket> _packets = new();
    private readonly Label _statusLabel;
    private readonly Button _toggleButton;
    private readonly Button _clearButton;
    private bool _isCapturing;

    public SnifferView(SnifferService sniffer)
    {
        _sniffer = sniffer;
        _sniffer.PacketsFlushed += OnPacketsFlushed;

        // Title
        var titleLabel = new Label("Packet Sniffer")
        {
            X = Pos.Center(),
            Y = 1
        };

        // Toggle button
        _toggleButton = new Button("_Start Capture")
        {
            X = 2,
            Y = 3
        };
        _toggleButton.Clicked += OnToggleCapture;

        // Clear button
        _clearButton = new Button("_Clear")
        {
            X = Pos.Right(_toggleButton) + 2,
            Y = 3
        };
        _clearButton.Clicked += OnClear;

        // Status
        _statusLabel = new Label("Status: Stopped")
        {
            X = 20,
            Y = 3,
            Width = Dim.Fill(2)
        };

        // Packet list
        var packetFrame = new FrameView("Captured Packets")
        {
            X = 2,
            Y = 5,
            Width = Dim.Fill(2),
            Height = Dim.Fill(2)
        };

        _packetListView = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _packetListView.SetSource(_packets);
        packetFrame.Add(_packetListView);

        Add(titleLabel, _toggleButton, _clearButton, _statusLabel, packetFrame);
    }

    private void OnToggleCapture()
    {
        if (_isCapturing)
        {
            _sniffer.StopCapture();
            _toggleButton.Text = "_Start Capture";
            _statusLabel.Text = "Status: Stopped";
            _isCapturing = false;
        }
        else
        {
            _sniffer.StartCapture();
            _toggleButton.Text = "_Stop Capture";
            _statusLabel.Text = "Status: Capturing...";
            _isCapturing = true;
        }
    }

    private void OnClear()
    {
        _packets.Clear();
        _packetListView.SetSource(_packets);
        _packetListView.SetNeedsDisplay();
    }

    private void OnPacketsFlushed(object? sender, List<CapturedPacket> packets)
    {
        Application.MainLoop?.Invoke(() =>
        {
            foreach (var packet in packets)
            {
                _packets.Insert(0, packet);
            }

            // Keep only last 1000 packets
            if (_packets.Count > 1000)
                _packets.RemoveRange(1000, _packets.Count - 1000);

            _packetListView.SetSource(_packets);
            _packetListView.SetNeedsDisplay();
        });
    }
}
