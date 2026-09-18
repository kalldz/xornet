using Terminal.Gui;
using Xornet.Engine;
using Xornet.Models;

namespace Xornet.UI;

public class ClientListView : View
{
    private readonly Scanner _scanner;
    private readonly ListView _listView;
    private readonly List<ClientWrapper> _displayList = new();

    public event EventHandler<Client>? ClientSelected;

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
        _listView.OpenSelectedItem += (e) => OnItemSelected(this, e);

        container.Add(_listView);
        Add(container);

        Refresh();
    }

    public Client? GetSelectedClient()
    {
        if (_listView.SelectedItem < 0 || _listView.SelectedItem >= _displayList.Count)
            return null;
        return _displayList[_listView.SelectedItem].Client;
    }

    public void Refresh()
    {
        _displayList.Clear();
        var clients = _scanner.GetClients()
            .Values
            .OrderByDescending(c => c.IsOnline)
            .ThenBy(c => c.IsKilled)
            .ThenBy(c => c.Ip.ToString())
            .ToList();

        _displayList.AddRange(clients.Select(c => new ClientWrapper(c)));
        _listView.SetSource(_displayList);
        _listView.SetNeedsDisplay();
    }

    private void OnItemSelected(object? sender, ListViewItemEventArgs e)
    {
        if (e.Value is ClientWrapper wrapper)
        {
            ClientSelected?.Invoke(this, wrapper.Client);
        }
    }
}

public class ClientWrapper
{
    public Client Client { get; }

    public ClientWrapper(Client client)
    {
        Client = client;
    }

    public override string ToString()
    {
        var status = Client.IsOnline ? "ON" : "OFF";
        var killed = Client.IsKilled ? " [KILLED]" : "";
        var type = Client.Type.ToString().ToUpperInvariant();
        var name = Client.Name != "Unknown" ? $" ({Client.Name})" : "";
        var vendor = Client.Vendor != "NA" ? $" [{Client.Vendor}]" : "";

        return $"[{status}] {Client.Ip,-15} {Client.GetMacString(),-17} {type}{name}{vendor}{killed}";
    }
}
