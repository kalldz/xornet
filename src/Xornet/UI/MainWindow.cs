using Terminal.Gui;
using Xornet.Engine;
using Xornet.Services;

namespace Xornet.UI;

public class MainWindow : Window
{
    private readonly Scanner _scanner;
    private readonly ClientListView _clientListView;
    private readonly StatusBar _statusBar;
    private readonly Label _titleLabel;
    private readonly Button _scanButton;
    private readonly Label _statusLabel;
    private readonly Label _countLabel;

    public MainWindow(Scanner scanner) : base("Xornet - Network Commander")
    {
        _scanner = scanner;
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

        // Status
        _statusLabel = new Label("Status: Ready")
        {
            X = 20,
            Y = 3,
            Width = Dim.Fill(2)
        };

        // Count
        _countLabel = new Label("Devices: 0")
        {
            X = 2,
            Y = 5
        };

        // Client list
        _clientListView = new ClientListView(_scanner)
        {
            X = 2,
            Y = 7,
            Width = Dim.Fill(2),
            Height = Dim.Fill(4)
        };

        // Status bar
        _statusBar = new StatusBar(new StatusItem[]
        {
            new StatusItem(Key.F2, "~F2~ Scan", OnScanClicked),
            new StatusItem(Key.F10, "~F10~ Quit", () => Application.RequestStop()),
            new StatusItem(Key.CharMask, $"Adapter: {HostInfo.NetworkAdapterName ?? "Unknown"}", null!)
        });

        Add(_titleLabel, _scanButton, _statusLabel, _countLabel, _clientListView);
        Application.Top?.Add(_statusBar);

        // Auto-start scan
        _ = AutoScanAsync();
    }

    private async Task AutoScanAsync()
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

    private void UpdateCount()
    {
        var online = _scanner.GetClients().Values.Count(c => c.IsOnline);
        var total = _scanner.GetClients().Count;
        _countLabel.Text = $"Devices: {online} online / {total} total";
    }
}
