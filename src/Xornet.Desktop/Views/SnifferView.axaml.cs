using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Xornet.Desktop.Views;

public partial class SnifferView : UserControl
{
    public SnifferView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
