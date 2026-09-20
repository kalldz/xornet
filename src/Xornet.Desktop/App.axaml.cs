using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Xornet.Data;
using Xornet.Desktop.ViewModels;
using Xornet.Desktop.Views;
using Xornet.Engine;
using Xornet.Services;

namespace Xornet.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            InitializeServices();

            var mainWindow = Services.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;

            desktop.ShutdownRequested += (_, _) =>
            {
                var scanner = Services.GetRequiredService<Scanner>();
                var killer = Services.GetRequiredService<Killer>();
                var defender = Services.GetRequiredService<Defender>();
                var detector = Services.GetRequiredService<ArpSpoofDetector>();
                var sniffer = Services.GetRequiredService<SnifferService>();
                var bandwidth = Services.GetRequiredService<BandwidthService>();

                scanner.PacketArrived -= bandwidth.OnPacketArrival;
                bandwidth.Stop();
                sniffer.StopCapture();
                detector.Stop();
                defender.Stop();
                killer.Stop();
                scanner.Stop();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void InitializeServices()
    {
        var deviceManager = Services.GetRequiredService<DeviceManager>();
        var nameResolver = Services.GetRequiredService<NameResolver>();
        var scanner = Services.GetRequiredService<Scanner>();
        var killer = Services.GetRequiredService<Killer>();
        var defender = Services.GetRequiredService<Defender>();
        var detector = Services.GetRequiredService<ArpSpoofDetector>();
        var sniffer = Services.GetRequiredService<SnifferService>();
        var bandwidth = Services.GetRequiredService<BandwidthService>();

        // Show network picker if no device saved
        var savedDevice = DataStore.LoadDeviceName();
        if (string.IsNullOrEmpty(savedDevice))
        {
            var picker = new NetworkPickerWindow(deviceManager.GetAllDevices());
            picker.ShowDialog(Services.GetRequiredService<MainWindow>());

            if (!picker.IsCancelled && !string.IsNullOrEmpty(picker.SelectedDeviceName))
            {
                deviceManager.ChangeDevice(picker.SelectedDeviceName);
            }
        }

        // Start services
        scanner.Start();
        killer.Start();
        bandwidth.Start();
        scanner.PacketArrived += bandwidth.OnPacketArrival;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<DeviceManager>();
        services.AddSingleton<NameResolver>();
        services.AddSingleton<Scanner>();
        services.AddSingleton<Killer>();
        services.AddSingleton<Defender>();
        services.AddSingleton<ArpSpoofDetector>();
        services.AddSingleton<SnifferService>();
        services.AddSingleton<BandwidthService>();

        // ViewModels
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<ShieldViewModel>();
        services.AddSingleton<SnifferViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
    }
}