using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Xornet.Engine;
using Xornet.Services;

namespace Xornet.Desktop.ViewModels;

public enum NavigationPage
{
    Dashboard,
    Shield,
    Sniffer
}

public partial class MainWindowViewModel : ObservableObject
{
    private readonly Scanner _scanner;

    [ObservableProperty]
    private NavigationPage _currentPage = NavigationPage.Dashboard;

    [ObservableProperty]
    private object _currentView;

    [ObservableProperty]
    private DashboardViewModel _dashboardViewModel;

    [ObservableProperty]
    private ShieldViewModel _shieldViewModel;

    [ObservableProperty]
    private SnifferViewModel _snifferViewModel;

    [ObservableProperty]
    private string _adapterName = "Unknown";

    [ObservableProperty]
    private int _deviceCount;

    public MainWindowViewModel(
        Scanner scanner,
        Killer killer,
        Defender defender,
        ArpSpoofDetector detector,
        SnifferService sniffer,
        DashboardViewModel dashboardVm,
        ShieldViewModel shieldVm,
        SnifferViewModel snifferVm)
    {
        _scanner = scanner;

        _dashboardViewModel = dashboardVm;
        _shieldViewModel = shieldVm;
        _snifferViewModel = snifferVm;

        _currentView = _dashboardViewModel;
        _adapterName = HostInfo.NetworkAdapterName ?? "Unknown";
        _scanner.ClientsChanged += OnClientsChanged;
    }

    private void OnClientsChanged(object? sender, EventArgs e)
    {
        DeviceCount = _scanner.GetClients().Count;
    }

    [RelayCommand]
    private void Navigate(string page)
    {
        CurrentPage = Enum.Parse<NavigationPage>(page);
        CurrentView = CurrentPage switch
        {
            NavigationPage.Dashboard => DashboardViewModel,
            NavigationPage.Shield => ShieldViewModel,
            NavigationPage.Sniffer => SnifferViewModel,
            _ => DashboardViewModel
        };
    }
}
