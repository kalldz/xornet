using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Xornet.Engine;
using Xornet.Models;

namespace Xornet.Desktop.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly Scanner _scanner;
    private readonly Killer _killer;

    [ObservableProperty]
    private ObservableCollection<ClientViewModel> _clients = new();

    [ObservableProperty]
    private ClientViewModel? _selectedClient;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private int _totalDevices;

    [ObservableProperty]
    private int _onlineDevices;

    [ObservableProperty]
    private int _killedDevices;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public DashboardViewModel(Scanner scanner, Killer killer)
    {
        _scanner = scanner;
        _killer = killer;
        _scanner.ClientsChanged += OnClientsChanged;

        RefreshClients();
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshClients();
    }

    private void OnClientsChanged(object? sender, EventArgs e)
    {
        RefreshClients();
    }

    private void RefreshClients()
    {
        var allClients = _scanner.GetClients().Values
            .OrderByDescending(c => c.IsOnline)
            .ThenBy(c => c.IsKilled)
            .ThenBy(c => c.Ip.ToString());

        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? allClients
            : allClients.Where(c =>
                c.Ip.ToString().Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                c.GetFormattedMacString().Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                c.Vendor.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        var viewModels = filtered.Select(c => new ClientViewModel(c)).ToList();
        foreach (var vm in viewModels) vm.Refresh();

        Clients = new ObservableCollection<ClientViewModel>(viewModels);
        TotalDevices = viewModels.Count;
        OnlineDevices = viewModels.Count(c => c.IsOnline);
        KilledDevices = viewModels.Count(c => c.IsKilled);
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsScanning = true;
        StatusText = "Scanning...";
        await _scanner.ScanAsync();
        IsScanning = false;
        StatusText = "Ready";
    }

    [RelayCommand]
    private void Kill()
    {
        if (SelectedClient == null) return;
        var client = _scanner.GetClients().Values
            .FirstOrDefault(c => c.Ip.ToString() == SelectedClient.IpAddress);
        if (client != null)
        {
            _killer.Kill(client);
            RefreshClients();
        }
    }

    [RelayCommand]
    private void KillAll()
    {
        foreach (var client in _scanner.GetClients().Values.Where(c => !c.IsKilled))
        {
            _killer.Kill(client);
        }
        RefreshClients();
    }

    [RelayCommand]
    private void Restore()
    {
        if (SelectedClient == null) return;
        var client = _scanner.GetClients().Values
            .FirstOrDefault(c => c.Ip.ToString() == SelectedClient.IpAddress);
        if (client != null)
        {
            _killer.UnKill(client);
            RefreshClients();
        }
    }

    [RelayCommand]
    private void RestoreAll()
    {
        _killer.UnKillAll();
        RefreshClients();
    }
}
