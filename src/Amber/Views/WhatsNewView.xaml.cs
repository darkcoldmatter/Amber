using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Amber.Views;

/// <summary>
/// "New in this version": the bullets of <see cref="ReleaseNotes"/>, shown once after an update, over the whole window.
/// Close or Esc closes it; clicking around it does nothing, and keys don't reach the window behind.
/// </summary>
public partial class WhatsNewView : UserControl
{
    public WhatsNewView(IReadOnlyList<string> bullets)
    {
        InitializeComponent();
        Bullets.ItemsSource = bullets;
        Loaded += (_, _) => CloseButton.Focus();
    }

    public event EventHandler? CloseRequested;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Close();
        // Delete, Esc and the rest would otherwise go on to the window and act on what's behind (a selected filter).
        e.Handled = true;
    }

    void OnClose(object sender, RoutedEventArgs e) => Close();

    void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
