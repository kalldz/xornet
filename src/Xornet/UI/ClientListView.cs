using Terminal.Gui;
using Xornet.Engine;
using Xornet.Models;

namespace Xornet.UI;

public class ClientListView : View
{
    private readonly Scanner _scanner;
    private readonly ListView _listView;
    private readonly List<Client> _displayList = new();

    public ClientListView(Scanner scanner)
    {
        _scanner = scanner;

        var container = new FrameView("Discovered Devices")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        _listView = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _listView.SetSource(_displayList);

        container.Add(_listView);
        Add(container);

        Refresh();
    }

    public void Refresh()
    {
        _displayList.Clear();
        var clients = _scanner.GetClients()
            .Values
            .OrderByDescending(c => c.IsOnline)
            .ThenBy(c => c.Ip.ToString())
            .ToList();

        _displayList.AddRange(clients);
        _listView.SetSource(_displayList);
        _listView.SetNeedsDisplay();
    }
}

public class ClientWrapper
{
    private readonly Client _client;

    public ClientWrapper(Client client)
    {
        _client = client;
    }

    public override string ToString()
    {
        var status = _client.IsOnline ? "ON" : "OFF";
        var killed = _client.IsKilled ? " [KILLED]" : "";
        var type = _client.Type.ToString().ToUpperInvariant();
        var name = _client.Name != "Unknown" ? $" ({_client.Name})" : "";
        var vendor = _client.Vendor != "NA" ? $" [{_client.Vendor}]" : "";

        return $"[{status}] {_client.Ip,-15} {_client.GetMacString(),-17} {type}{name}{vendor}{killed}";
    }
}
