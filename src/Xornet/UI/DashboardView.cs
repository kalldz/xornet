using Terminal.Gui;
using Xornet.Engine;
using Xornet.Models;
using Xornet.Services;

namespace Xornet.UI;

public class DashboardView : View
{
    private readonly Scanner _scanner;
    private readonly Killer _killer;

    private readonly Label _titleLabel;
    private readonly Button _scanButton;
    private readonly Button _killButton;
    private readonly Button _restoreButton;
    private readonly Button _restoreAllButton;
    private readonly Label _statusLabel;
    private readonly Label _countLabel;
    private readonly Label _killedLabel;
    private readonly ClientListView _clientListView;

    public DashboardView(Scanner scanner, Killer killer)
    {
        _scanner = scanner;
        _killer = killer;
        _scanner.ClientsChanged += OnClientsChanged;

        // Title
        _titleLabel = new Label("Xornet Network Scanner")
        {
            X = Pos.Center(),
            Y = 1
        };

        // Scan button
        _scanButton = new Button("_Scan Network")
        {
            X = 2,
            Y = 3
        };
        _scanButton.Clicked += OnScanClicked;

        // Kill button
        _killButton = new Button("_Kill")
        {
            X = Pos.Right(_scanButton) + 2,
            Y = 3
        };
        _killButton.Clicked += OnKillClicked;

        // Restore button
        _restoreButton = new Button("_Restore")
        {
            X = Pos.Right(_killButton) + 2,
            Y = 3
        };
        _restoreButton.Clicked += OnRestoreClicked;

        // Restore All button
        _restoreAllButton = new Button("Restore _All")
        {
            X = Pos.Right(_restoreButton) + 2,
            Y = 3
        };
        _restoreAllButton.Clicked += OnRestoreAllClicked;

        // Status
        _statusLabel = new Label("Status: Ready")
        {
            X = 2,
            Y = 5,
            Width = Dim.Fill(2)
        };

        // Count
        _countLabel = new Label("Devices: 0")
        {
            X = 2,
            Y = 6
        };

        // Killed count
        _killedLabel = new Label("Killed: 0")
        {
            X = 25,
            Y = 6
        };

        // Client list
        _clientListView = new ClientListView(_scanner)
        {
            X = 2,
            Y = 8,
            Width = Dim.Fill(2),
            Height = Dim.Fill(4)
        };
        _clientListView.ClientSelected += OnClientSelected;

        Add(_titleLabel, _scanButton, _killButton, _restoreButton, _restoreAllButton,
            _statusLabel, _countLabel, _killedLabel, _clientListView);

        Refresh();
    }

    public async Task AutoScanAsync()
    {
        _statusLabel.Text = "Status: Scanning...";
        await _scanner.ScanAsync();
        _statusLabel.Text = "Status: Ready";
        UpdateCount();
    }

    private async void OnScanClicked()
    {
        _scanButton.Enabled = false;
        _statusLabel.Text = "Status: Scanning...";
        await _scanner.ScanAsync();
        _statusLabel.Text = "Status: Ready";
        _scanButton.Enabled = true;
        UpdateCount();
    }

    private void OnClientsChanged(object? sender, EventArgs e)
    {
        Application.MainLoop?.Invoke(() =>
        {
            _clientListView.Refresh();
            UpdateCount();
        });
    }

    private void OnKillClicked()
    {
        var client = _clientListView.GetSelectedClient();
        if (client == null)
        {
            _statusLabel.Text = "Status: No device selected";
            return;
        }

        if (client.IsGateway() || client.IsLocalDevice())
        {
            _statusLabel.Text = "Status: Cannot kill gateway or local device";
            return;
        }

        _killer.Kill(client);
        _statusLabel.Text = $"Status: Killing {client.Ip}";
        UpdateCount();
    }

    private void OnRestoreClicked()
    {
        var client = _clientListView.GetSelectedClient();
        if (client == null)
        {
            _statusLabel.Text = "Status: No device selected";
            return;
        }

        _killer.UnKill(client);
        _statusLabel.Text = $"Status: Restored {client.Ip}";
        UpdateCount();
    }

    private void OnRestoreAllClicked()
    {
        _killer.UnKillAll();
        _statusLabel.Text = "Status: Restored all devices";
        UpdateCount();
    }

    private void OnClientSelected(object? sender, Client client)
    {
        _statusLabel.Text = $"Status: Selected {client.Ip} [{client.GetMacString()}]";
    }

    private void UpdateCount()
    {
        var online = _scanner.GetClients().Values.Count(c => c.IsOnline);
        var total = _scanner.GetClients().Count;
        var killed = _scanner.GetClients().Values.Count(c => c.IsKilled);
        _countLabel.Text = $"Devices: {online} online / {total} total";
        _killedLabel.Text = $"Killed: {killed}";
    }

    public void Refresh()
    {
        _clientListView.Refresh();
        UpdateCount();
    }
}
