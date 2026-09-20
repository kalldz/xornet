using CommunityToolkit.Mvvm.ComponentModel;
using Xornet.Models;

namespace Xornet.Desktop.ViewModels;

public partial class ClientViewModel : ObservableObject
{
    private readonly Client _client;

    public ClientViewModel(Client client)
    {
        _client = client;
    }

    public string IpAddress => _client.Ip.ToString();
    public string MacAddress => _client.GetFormattedMacString();
    public string Hostname => _client.Name;
    public string Vendor => _client.Vendor;
    public bool IsGateway => _client.IsGateway();
    public bool IsLocalDevice => _client.IsLocalDevice();
    public DateTime FirstSeen => _client.FirstSeen;

    [ObservableProperty]
    private bool _isOnline;

    [ObservableProperty]
    private bool _isKilled;

    public void Refresh()
    {
        IsOnline = _client.IsOnline;
        IsKilled = _client.IsKilled;
    }
}
