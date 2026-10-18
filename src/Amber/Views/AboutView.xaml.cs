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
        // The full version, "1.0.0" (or "1.1.0-beta.1"), without the build's commit id that follows a "+".
        VersionRun.Text = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];
    }

    public event EventHandler? CloseRequested;

    void OnGitHubClick(object sender, RoutedEventArgs e) => Open(RepositoryUrl);

    // The issue forms in .github/ISSUE_TEMPLATE.
    void OnReportBugClick(object sender, RoutedEventArgs e) => Open(RepositoryUrl + "/issues/new?template=bug_report.yml");

    void OnSuggestIdeaClick(object sender, RoutedEventArgs e) => Open(RepositoryUrl + "/issues/new?template=idea.yml");

    void OnLicensesClick(object sender, RoutedEventArgs e) => Open(Path.Combine(AppContext.BaseDirectory, "Licenses.txt"));

    void OnClose(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
}
