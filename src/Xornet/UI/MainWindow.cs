using Terminal.Gui;
using Xornet.Engine;
using Xornet.Models;
using Xornet.Services;

namespace Xornet.UI;

public class MainWindow : Window
{
    private readonly Scanner _scanner;
    private readonly Killer _killer;
    private readonly Defender _defender;
    private readonly ArpSpoofDetector _detector;
    private readonly SnifferService _sniffer;

    private readonly TabView _tabView;
    private readonly StatusBar _statusBar;

    public MainWindow(Scanner scanner, Killer killer, Defender defender, ArpSpoofDetector detector, SnifferService sniffer)
        : base("Xornet - Network Commander")
    {
        _scanner = scanner;
        _killer = killer;
        _defender = defender;
        _detector = detector;
        _sniffer = sniffer;
        _scanner.ClientsChanged += OnClientsChanged;

        // Tab view for navigation
        _tabView = new TabView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1)
        };

        // Dashboard tab
        var dashboardView = new DashboardView(_scanner, _killer);
        var dashboardTab = new TabView.Tab("Dashboard", dashboardView);
        _tabView.AddTab(dashboardTab, true);

        // Shield tab
        var shieldView = new ShieldView(_scanner, _defender, _detector);
        var shieldTab = new TabView.Tab("Shield", shieldView);
        _tabView.AddTab(shieldTab, false);

        // Sniffer tab
        var snifferView = new SnifferView(_sniffer);
        var snifferTab = new TabView.Tab("Sniffer", snifferView);
        _tabView.AddTab(snifferTab, false);

        // Status bar
        _statusBar = new StatusBar(new StatusItem[]
        {
            new StatusItem(Key.F1, "~F1~ Dashboard", () => _tabView.SelectedTab = dashboardTab),
            new StatusItem(Key.F2, "~F2~ Shield", () => _tabView.SelectedTab = shieldTab),
            new StatusItem(Key.F3, "~F3~ Sniffer", () => _tabView.SelectedTab = snifferTab),
            new StatusItem(Key.F10, "~F10~ Quit", () => Application.RequestStop()),
            new StatusItem(Key.CharMask, $"Adapter: {HostInfo.NetworkAdapterName ?? "Unknown"}", null!)
        });

        Add(_tabView);
        Application.Top?.Add(_statusBar);

        // Auto-start scan
        _ = dashboardView.AutoScanAsync();
    }

    private void OnClientsChanged(object? sender, EventArgs e)
    {
        Application.MainLoop?.Invoke(() =>
        {
            foreach (var tab in _tabView.Tabs)
            {
                if (tab.View is DashboardView dv) dv.Refresh();
                if (tab.View is ShieldView sv) sv.Refresh();
            }
        });
    }
}
