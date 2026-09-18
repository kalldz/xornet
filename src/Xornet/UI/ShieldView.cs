using Terminal.Gui;
using Xornet.Engine;
using Xornet.Models;

namespace Xornet.UI;

public class ShieldView : View
{
    private readonly Scanner _scanner;
    private readonly Defender _defender;
    private readonly ArpSpoofDetector _detector;

    private readonly Label _titleLabel;
    private readonly Button _defenderToggleButton;
    private readonly Button _detectorToggleButton;
    private readonly Label _defenderStatusLabel;
    private readonly Label _detectorStatusLabel;
    private readonly Label _spoofingStatusLabel;
    private readonly ListView _protectedListView;
    private readonly ListView _attackersListView;
    private readonly List<ClientWrapper> _protectedList = new();
    private readonly List<string> _attackersList = new();

    public ShieldView(Scanner scanner, Defender defender, ArpSpoofDetector detector)
    {
        _scanner = scanner;
        _defender = defender;
        _detector = detector;

        _defender.SpoofingDetectedChanged += OnSpoofingDetectedChanged;
        _detector.AttackDetected += OnAttackDetected;

        // Title
        _titleLabel = new Label("Shield - ARP Defense")
        {
            X = Pos.Center(),
            Y = 1
        };

        // Defender toggle
        _defenderToggleButton = new Button("Toggle _Defender")
        {
            X = 2,
            Y = 3
        };
        _defenderToggleButton.Clicked += OnToggleDefender;

        _defenderStatusLabel = new Label("Defender: OFF")
        {
            X = 25,
            Y = 3,
            Width = Dim.Fill(2)
        };

        // Detector toggle
        _detectorToggleButton = new Button("Toggle _Detector")
        {
            X = 2,
            Y = 5
        };
        _detectorToggleButton.Clicked += OnToggleDetector;

        _detectorStatusLabel = new Label("Detector: OFF")
        {
            X = 25,
            Y = 5,
            Width = Dim.Fill(2)
        };

        // Spoofing status
        _spoofingStatusLabel = new Label("Spoofing Detected: No")
        {
            X = 2,
            Y = 7,
            Width = Dim.Fill(2)
        };

        // Attackers list
        var attackersFrame = new FrameView("Detected Attackers")
        {
            X = 2,
            Y = 9,
            Width = Dim.Fill(2),
            Height = Dim.Percent(30)
        };

        _attackersListView = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _attackersListView.SetSource(_attackersList);
        attackersFrame.Add(_attackersListView);

        // Protected devices list
        var protectedFrame = new FrameView("Protected Devices")
        {
            X = 2,
            Y = Pos.Bottom(attackersFrame) + 1,
            Width = Dim.Fill(2),
            Height = Dim.Fill(2)
        };

        _protectedListView = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _protectedListView.SetSource(_protectedList);
        protectedFrame.Add(_protectedListView);

        Add(_titleLabel, _defenderToggleButton, _defenderStatusLabel,
            _detectorToggleButton, _detectorStatusLabel, _spoofingStatusLabel,
            attackersFrame, protectedFrame);

        RefreshProtectedList();
    }

    private void OnToggleDefender()
    {
        if (_defender.IsDefending)
        {
            _defender.Stop();
            _defenderStatusLabel.Text = "Defender: OFF";
        }
        else
        {
            _defender.Defend();
            _defenderStatusLabel.Text = "Defender: ON";
        }
        UpdateSpoofingStatus();
    }

    private void OnToggleDetector()
    {
        if (_detector.IsRunning)
        {
            _detector.Stop();
            _detectorStatusLabel.Text = "Detector: OFF";
        }
        else
        {
            _detector.Start();
            _detectorStatusLabel.Text = "Detector: ON";
        }
        RefreshProtectedList();
    }

    private void OnSpoofingDetectedChanged(object? sender, EventArgs e)
    {
        Application.MainLoop?.Invoke(UpdateSpoofingStatus);
    }

    private void OnAttackDetected(object? sender, AttackDetectedEventArgs e)
    {
        Application.MainLoop?.Invoke(() =>
        {
            var entry = $"{e.DetectedAt:HH:mm:ss} - MAC: {e.AttackerMac}";
            if (e.AttackerIp != null)
                entry += $" IP: {e.AttackerIp}";

            _attackersList.Insert(0, entry);
            _attackersListView.SetSource(_attackersList);
            _attackersListView.SetNeedsDisplay();
            UpdateSpoofingStatus();
        });
    }

    private void UpdateSpoofingStatus()
    {
        var detected = _defender.SpoofingDetected || _detector.AttackerMacs.Count > 0;
        _spoofingStatusLabel.Text = $"Spoofing Detected: {(detected ? "YES - ATTACK IN PROGRESS" : "No")}";
        _spoofingStatusLabel.ColorScheme = detected
            ? new ColorScheme { Normal = Terminal.Gui.Attribute.Make(Color.BrightRed, Color.Black) }
            : null;
    }

    private void RefreshProtectedList()
    {
        _protectedList.Clear();
        var clients = _scanner.GetClients().Values.ToList();

        foreach (var client in clients)
        {
            var mac = client.GetMacString();
            if (_detector.IsProtected(mac))
            {
                _protectedList.Add(new ClientWrapper(client));
            }
        }

        _protectedListView.SetSource(_protectedList);
        _protectedListView.SetNeedsDisplay();
    }

    public void Refresh()
    {
        RefreshProtectedList();
        UpdateSpoofingStatus();
    }
}
