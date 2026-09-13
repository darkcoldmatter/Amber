using System.Windows;

namespace Amber;

public partial class MainWindow : Window
{
    public MainWindow(Settings settings)
    {
        InitializeComponent();
        DataContext = settings;
    }
}
