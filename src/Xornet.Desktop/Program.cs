using System.Runtime.InteropServices;
using System.Security.Principal;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Xornet.Data;
using Xornet.Desktop.Views;

namespace Xornet.Desktop;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Admin/root check
        if (!IsRunningAsAdmin())
        {
            ShowAdminError();
            Environment.Exit(1);
            return;
        }

        // Ensure data directory exists
        Directory.CreateDirectory(DataStore.DataPath);

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    private static bool IsRunningAsAdmin()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
        else
        {
            return Environment.UserName == "root" || geteuid() == 0;
        }
    }

    [DllImport("libc")]
    private static extern uint geteuid();

    private static void ShowAdminError()
    {
        // Try to show a simple message dialog if Avalonia is available
        try
        {
            var dialog = new Window
            {
                Title = "Xornet - Error",
                Width = 400,
                Height = 200,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Administrator privileges required.",
                            FontSize = 16,
                            FontWeight = Avalonia.Media.FontWeight.Bold,
                            Margin = new Avalonia.Thickness(0, 0, 0, 10)
                        },
                        new TextBlock
                        {
                            Text = "Windows: Right-click → Run as Administrator\nLinux: sudo ./Xornet",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        }
                    }
                }
            };
            dialog.Show();
        }
        catch
        {
            Console.WriteLine("Error: Administrator privileges required.");
            Console.WriteLine("Windows: Right-click → Run as Administrator");
            Console.WriteLine("Linux: sudo ./Xornet");
        }
    }
}
