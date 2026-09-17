using System.Collections.Concurrent;
using System.Text.Json;
using Xornet.Models;

namespace Xornet.Data;

public static class DataStore
{
    public static string DataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xornet");

    private static readonly string ClientsFile = Path.Combine(DataPath, "clients.json");
    private static readonly string DeviceFile = Path.Combine(DataPath, "device.json");

    private static IList<SerializedClient>? _clientsCache;
    private static DateTime _clientsCacheTime = DateTime.MinValue;
    private static readonly object _cacheLock = new();
    private const int CacheValidityMs = 5000;

    static DataStore()
    {
        Directory.CreateDirectory(DataPath);
    }

    public static IList<SerializedClient> LoadClients()
    {
        lock (_cacheLock)
        {
            if (_clientsCache != null &&
                (DateTime.UtcNow - _clientsCacheTime).TotalMilliseconds < CacheValidityMs)
            {
                return _clientsCache;
            }
        }

        if (!File.Exists(ClientsFile))
        {
            var empty = new List<SerializedClient>();
            UpdateCache(empty);
            return empty;
        }

        try
        {
            var json = File.ReadAllText(ClientsFile);
            var clients = JsonSerializer.Deserialize<List<SerializedClient>>(json) ?? new List<SerializedClient>();
            UpdateCache(clients);
            return clients;
        }
        catch
        {
            var empty = new List<SerializedClient>();
            UpdateCache(empty);
            return empty;
        }
    }

    public static void SaveClients(IList<SerializedClient> clients)
    {
        lock (_cacheLock)
        {
            _clientsCache = clients.ToList();
            _clientsCacheTime = DateTime.UtcNow;
        }

        Task.Run(async () =>
        {
            await Task.Delay(1000);
            try
            {
                var json = JsonSerializer.Serialize(clients, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(ClientsFile, json);
            }
            catch { /* ignore write errors */ }
        });
    }

    public static string? LoadDeviceName()
    {
        if (!File.Exists(DeviceFile))
            return null;

        try
        {
            return File.ReadAllText(DeviceFile).Trim();
        }
        catch
        {
            return null;
        }
    }

    public static void SaveDeviceName(string deviceName)
    {
        try
        {
            File.WriteAllText(DeviceFile, deviceName);
        }
        catch { /* ignore */ }
    }

    private static void UpdateCache(IList<SerializedClient> clients)
    {
        lock (_cacheLock)
        {
            _clientsCache = clients;
            _clientsCacheTime = DateTime.UtcNow;
        }
    }
}
