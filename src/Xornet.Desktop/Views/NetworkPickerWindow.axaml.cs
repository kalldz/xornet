using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SharpPcap.LibPcap;

namespace Xornet.Desktop.Views;

public partial class NetworkPickerWindow : Window
{
    private readonly List<LibPcapLiveDevice> _devices = new();

    public string? SelectedDeviceName { get; private set; }
    public bool IsCancelled { get; private set; } = true;

    public NetworkPickerWindow()
    {
        InitializeComponent();
    }

    public NetworkPickerWindow(IEnumerable<LibPcapLiveDevice> devices) : this()
    {
        _devices = devices.ToList();
        var listBox = this.FindControl<ListBox>("DeviceListBox");
        var selectButton = this.FindControl<Button>("SelectButton");
        var cancelButton = this.FindControl<Button>("CancelButton");

        if (listBox != null)
        {
            listBox.ItemsSource = _devices.Select(d => new DeviceItem
            {
                FriendlyName = d.Interface.FriendlyName,
                Description = d.Interface.Description,
                Gateway = d.Interface.GatewayAddresses.Count > 0
                    ? d.Interface.GatewayAddresses[0].ToString()
                    : "No gateway",
                Device = d
            }).ToList();
        }

        if (selectButton != null)
            selectButton.Click += OnSelect;
        if (cancelButton != null)
            cancelButton.Click += OnCancel;
    }

    public class DeviceItem
    {
        public string FriendlyName { get; set; } = "";
        public string Description { get; set; } = "";
        public string Gateway { get; set; } = "";
        public LibPcapLiveDevice Device { get; set; } = null!;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnSelect(object? sender, RoutedEventArgs e)
    {
        var listBox = this.FindControl<ListBox>("DeviceListBox");
        if (listBox?.SelectedItem is DeviceItem selected)
        {
            SelectedDeviceName = selected.Device.Name;
            IsCancelled = false;
        }
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        IsCancelled = true;
        Close();
    }
}
