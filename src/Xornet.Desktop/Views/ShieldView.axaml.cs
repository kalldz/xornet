using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Xornet.Desktop.Views;

public partial class ShieldView : UserControl
{
    public ShieldView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
