using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Amber.Views;

public partial class AboutView : UserControl
{
    const string RepositoryUrl = "https://github.com/darkcoldmatter/Amber";

    public AboutView()
    {
        InitializeComponent();
        VersionRun.Text = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(2);
    }

    public event EventHandler? CloseRequested;

    void OnGitHubClick(object sender, RoutedEventArgs e) => Open(RepositoryUrl);

    void OnLicensesClick(object sender, RoutedEventArgs e) => Open(Path.Combine(AppContext.BaseDirectory, "Licenses.txt"));

    void OnClose(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
}
