using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Xornet.Engine;

namespace Xornet.Desktop.ViewModels;

public partial class ShieldViewModel : ObservableObject
{
    private readonly Defender _defender;
    private readonly ArpSpoofDetector _detector;

    [ObservableProperty]
    private bool _isDefending;

    [ObservableProperty]
    private bool _isDetecting;

    [ObservableProperty]
    private bool _spoofingDetected;

    [ObservableProperty]
    private string _defenderStatus = "OFF";

    [ObservableProperty]
    private string _detectorStatus = "OFF";

    [ObservableProperty]
    private string _spoofingStatus = "No attacks detected";

    [ObservableProperty]
    private ObservableCollection<string> _attackers = new();

    [ObservableProperty]
    private ObservableCollection<string> _protectedDevices = new();

    [ObservableProperty]
    private ObservableCollection<string> _alerts = new();

    public ShieldViewModel(Defender defender, ArpSpoofDetector detector)
    {
        _defender = defender;
        _detector = detector;

        _defender.SpoofingDetectedChanged += OnSpoofingDetectedChanged;
        _detector.AttackDetected += OnAttackDetected;
    }

    private void OnSpoofingDetectedChanged(object? sender, EventArgs e)
    {
        SpoofingDetected = _defender.SpoofingDetected;
        SpoofingStatus = _defender.SpoofingDetected ? "SPOOFING DETECTED!" : "No attacks detected";
        if (_defender.SpoofingDetected)
        {
            Alerts.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Spoofing detected!");
        }
    }

    private void OnAttackDetected(object? sender, AttackDetectedEventArgs e)
    {
        var attackerInfo = $"{e.AttackerMac} ({e.AttackerIp ?? "Unknown IP"})";
        if (!Attackers.Contains(attackerInfo))
        {
            Attackers.Add(attackerInfo);
        }
        Alerts.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Attack from {attackerInfo}");
    }

    [RelayCommand]
    private void ToggleDefender()
    {
        if (IsDefending)
        {
            _defender.Stop();
            IsDefending = false;
            DefenderStatus = "OFF";
        }
        else
        {
            _defender.Defend();
            IsDefending = true;
            DefenderStatus = "ON";
        }
    }

    [RelayCommand]
    private void ToggleDetector()
    {
        if (IsDetecting)
        {
            _detector.Stop();
            IsDetecting = false;
            DetectorStatus = "OFF";
        }
        else
        {
            _detector.Start();
            IsDetecting = true;
            DetectorStatus = "ON";
        }
    }

    [RelayCommand]
    private void ClearAlerts()
    {
        Alerts.Clear();
    }
}
