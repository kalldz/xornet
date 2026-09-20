using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Xornet.Desktop.ViewModels;

namespace Xornet.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}