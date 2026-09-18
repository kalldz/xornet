using System.Collections.Concurrent;
using System.Net;
using Xornet.Models;

namespace Xornet.Services;

public class NameResolver
{
    private Dictionary<string, string>? _vendorDictionary;
    private readonly ConcurrentDictionary<string, string> _dnsCache = new();
    private string _ouiFilePath;
    private const int DNS_TIMEOUT_MS = 3000;

    public NameResolver(string? ouiFilePath = null)
    {
        _ouiFilePath = ouiFilePath ?? Path.Combine(AppContext.BaseDirectory, "assets", "oui-database.txt");
        LoadVendorDictionary();
    }

    public void ResolveVendorName(Client client)
    {
        if (_vendorDictionary == null)
        {
            client.Vendor = "NA";
            return;
        }

        var oui = client.GetOui();
        if (string.IsNullOrEmpty(oui))
        {
            client.Vendor = "NA";
            return;
        }

        if (_vendorDictionary.TryGetValue(oui, out var vendor))
            client.Vendor = vendor;
        else
            client.Vendor = "NA";
    }

    public void ResolveClientName(Client client)
    {
        _ = ResolveClientNameAsyncInternal(client)
            .ContinueWith(t =>
            {
                // Error handling - silently ignore DNS failures
            }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task ResolveClientNameAsyncInternal(Client client)
    {
        var ipString = client.Ip.ToString();

        // Check cache first
        if (_dnsCache.TryGetValue(ipString, out var cachedName))
        {
            client.Name = cachedName;
            return;
        }

        using var cts = new CancellationTokenSource(DNS_TIMEOUT_MS);
        try
        {
            var host = await Dns.GetHostEntryAsync(ipString, cts.Token);
            if (host?.HostName != null && !host.HostName.Equals(ipString, StringComparison.OrdinalIgnoreCase))
            {
                client.Name = host.HostName;
                _dnsCache.TryAdd(ipString, host.HostName);
            }
        }
        catch
        {
            // DNS resolution failed - keep "Unknown"
        }
    }

    private void LoadVendorDictionary()
    {
        if (!File.Exists(_ouiFilePath))
        {
            // Try alternative paths
            var altPaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "assets", "oui-database.txt"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "assets", "oui-database.txt"),
            };

            foreach (var alt in altPaths)
            {
                if (File.Exists(alt))
                {
                    _ouiFilePath = alt;
                    break;
                }
            }
        }

        if (!File.Exists(_ouiFilePath))
        {
            _vendorDictionary = new Dictionary<string, string>();
            return;
        }

        try
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadLines(_ouiFilePath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var parts = line.Split('|', 2);
                if (parts.Length == 2)
                {
                    var oui = parts[0].Trim().ToUpperInvariant();
                    var vendor = parts[1].Trim();
                    dict[oui] = vendor;
                }
            }
            _vendorDictionary = dict;
        }
        catch
        {
            _vendorDictionary = new Dictionary<string, string>();
        }
    }
}
