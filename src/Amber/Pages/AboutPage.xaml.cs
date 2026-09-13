using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Amber.Pages;

public partial class AboutPage : UserControl
{
    const string RepositoryUrl = "https://github.com/darkcoldmatter/Amber";

    public AboutPage()
    {
        InitializeComponent();
        VersionRun.Text = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(2);
    }

    void OnGitHubClick(object sender, RoutedEventArgs e) => Open(RepositoryUrl);

    void OnLicensesClick(object sender, RoutedEventArgs e) => Open(Path.Combine(AppContext.BaseDirectory, "Licenses.txt"));

    static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
}
