using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json.Serialization;
using Xornet.Models.Enums;

namespace Xornet.Models;

public class SerializedClient
{
    public string Ip { get; set; } = string.Empty;
    public string Mac { get; set; } = string.Empty;
    public string Name { get; set; } = "Unknown";
    public string Vendor { get; set; } = "NA";
    public ClientType Type { get; set; } = ClientType.Arp;
    public bool IsOnline { get; set; } = true;
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;

    public static SerializedClient FromClient(Client client)
    {
        return new SerializedClient
        {
            Ip = client.Ip.ToString(),
            Mac = client.GetMacString(),
            Name = client.Name,
            Vendor = client.Vendor,
            Type = client.Type,
            IsOnline = client.IsOnline,
            FirstSeen = client.FirstSeen
        };
    }

    public Client ToClient()
    {
        var ip = IPAddress.Parse(Ip);
        var mac = PhysicalAddress.Parse(Mac);
        var client = new Client(ip, mac)
        {
            Name = Name,
            Vendor = Vendor,
            Type = Type,
            IsOnline = IsOnline
        };
        return client;
    }
}
