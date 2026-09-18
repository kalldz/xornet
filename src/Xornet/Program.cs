using Terminal.Gui;
using Xornet.Data;
using Xornet.Engine;
using Xornet.Services;
using Xornet.UI;

namespace Xornet;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            // Ensure data directory exists
            Directory.CreateDirectory(DataStore.DataPath);

            // Initialize device manager (requires admin/root)
            DeviceManager deviceManager;
            try
            {
                deviceManager = new DeviceManager();
            }
            catch (NotSupportedException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("Please ensure you have the required permissions:");
                Console.WriteLine("  Windows: Run as Administrator with Npcap installed");
                Console.WriteLine("  Linux: Run with sudo and libpcap-dev installed");
                Environment.Exit(1);
                return;
            }

            // Initialize services
            var nameResolver = new NameResolver();

            // Initialize scanner
            using var scanner = new Scanner(deviceManager, nameResolver);
            scanner.Start();

            // Initialize killer
            using var killer = new Killer(scanner, deviceManager);
            killer.Start();

            // Initialize TUI
            Application.Init();
            Application.Run(new MainWindow(scanner, killer));
            Application.Shutdown();

            // Cleanup
            killer.Stop();
            scanner.Stop();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}
