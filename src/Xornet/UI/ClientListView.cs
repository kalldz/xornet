using System.Data;
using Terminal.Gui;
using Xornet.Engine;
using Xornet.Models;

namespace Xornet.UI;

public class ClientListView : View
{
    private readonly Scanner _scanner;
    private readonly TableView _tableView;
    private readonly DataTable _dataTable;
    private readonly List<Client> _clientList = new();

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

        _dataTable = new DataTable();
        _dataTable.Columns.Add("IP", typeof(string));
        _dataTable.Columns.Add("MAC", typeof(string));
        _dataTable.Columns.Add("Hostname", typeof(string));
        _dataTable.Columns.Add("Vendor", typeof(string));
        _dataTable.Columns.Add("Status", typeof(string));

        _tableView = new TableView(_dataTable)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        _tableView.Style.ShowHorizontalHeaderUnderline = true;
        _tableView.Style.ShowVerticalHeaderLines = true;

        _tableView.KeyPress += (e) =>
        {
            if (e.KeyEvent.Key == Key.Enter)
            {
                var client = GetSelectedClient();
                if (client != null)
                    ClientSelected?.Invoke(this, client);
                e.Handled = true;
            }
        };

        container.Add(_tableView);
        Add(container);

        Refresh();
    }

    public Client? GetSelectedClient()
    {
        if (_tableView.SelectedRow < 0 || _tableView.SelectedRow >= _clientList.Count)
            return null;
        return _clientList[_tableView.SelectedRow];
    }

    public void Refresh()
    {
        _clientList.Clear();
        _dataTable.Rows.Clear();

        var clients = _scanner.GetClients()
            .Values
            .OrderByDescending(c => c.IsOnline)
            .ThenBy(c => c.IsKilled)
            .ThenBy(c => c.Ip.ToString())
            .ToList();

        foreach (var client in clients)
        {
            _clientList.Add(client);
            _dataTable.Rows.Add(
                client.Ip.ToString(),
                client.GetFormattedMacString(),
                client.Name,
                client.Vendor,
                client.IsOnline ? "Online" : "Away"
            );
        }

        _tableView.SetNeedsDisplay();
    }
}
