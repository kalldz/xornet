using System.Text.Json;

namespace Xornet.Data;

public class XornetConfig
{
    public int ArpProbeIntervalMs { get; set; } = 30000;
    public int OfflineTimeoutSecs { get; set; } = 120;
    public int SpoofIntervalMs { get; set; } = 800;
    public int InitialBurstCount { get; set; } = 5;
    public int InitialBurstIntervalMs { get; set; } = 30;
    public int GatewayRefreshIntervalSeconds { get; set; } = 60;
    public int PingTimeoutMs { get; set; } = 1000;
    public int MaxConcurrency { get; set; } = 50;
    public int CacheValidityMs { get; set; } = 5000;
    public int DebounceSaveMs { get; set; } = 1000;
    public int FlushIntervalMs { get; set; } = 300;
    public int DnsTimeoutMs { get; set; } = 3000;
    public int ProtectIntervalMs { get; set; } = 800;
    public int DefenseIntervalMs { get; set; } = 500;
}

public static class ConfigStore
{
    private static readonly string ConfigFile = Path.Combine(DataStore.DataPath, "config.json");
    private static XornetConfig? _cached;

    public static XornetConfig Load()
    {
        if (_cached != null)
            return _cached;

        if (!File.Exists(ConfigFile))
        {
            _cached = new XornetConfig();
            Save(_cached);
            return _cached;
        }

        try
        {
            var json = File.ReadAllText(ConfigFile);
            _cached = JsonSerializer.Deserialize<XornetConfig>(json) ?? new XornetConfig();
            return _cached;
        }
        catch
        {
            _cached = new XornetConfig();
            return _cached;
        }
    }

    public static void Save(XornetConfig config)
    {
        _cached = config;
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(ConfigFile, json);
        }
        catch { /* ignore */ }
    }
}
