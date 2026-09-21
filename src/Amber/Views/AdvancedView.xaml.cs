using System.Windows;
using System.Windows.Controls;
using Amber.Tint;

namespace Amber.Views;

public partial class AdvancedView : UserControl
{
    static readonly TintMode[] Modes = Enum.GetValues<TintMode>();

    readonly Settings _settings;

    public AdvancedView(Settings settings)
    {
        InitializeComponent();
        _settings = settings;
        DataContext = settings;

        ShowFilters();
        settings.Filters.CollectionChanged += (_, _) => ShowFilters();
    }

    void ShowFilters()
    {
        var any = _settings.Filters.Count > 0;
        Timeline.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        HowItWorks.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        EmptyLabel.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        Timeline.Filters = _settings.Filters.ToArray();

        Cards.Children.Clear();
        foreach (var filter in _settings.Filters)
        {
            var card = new FilterCard(filter, _settings.Filters)
            {
                Margin = new Thickness(0, Cards.Children.Count == 0 ? 0 : 16, 0, 0),
            };
            card.DeleteRequested += (_, _) => _settings.Filters.Remove(filter);
            Cards.Children.Add(card);
        }

        AddButton.IsEnabled = HasRoom();
    }

    bool HasRoom() => FilterLayout.HasRoom(Ranges());

    IEnumerable<(int Start, int End)> Ranges() =>
        _settings.Filters.Select(f => (ScheduleRange.Minutes(f.Start), ScheduleRange.Minutes(f.End)));

    void OnAddClicked(object sender, RoutedEventArgs e)
    {
        AddChoices.Children.Clear();
        foreach (var mode in Modes)
        {
            var choice = new Button
            {
                Style = (Style)FindResource("ModeDropdownItem"),
                Content = NameOf(mode),
                Foreground = (System.Windows.Media.Brush)FindResource(mode + "Accent"),
                IsEnabled = HasRoom(),
            };
            choice.Click += (_, _) =>
            {
                AddMenu.IsOpen = false;
                Add(mode);
            };
            AddChoices.Children.Add(choice);
        }
        AddMenu.IsOpen = true;
    }

    /// <summary>A new filter takes the left-most free hour, or whatever is left of it.</summary>
    void Add(TintMode mode)
    {
        if (FilterLayout.NextSlot(Ranges()) is not var (start, end)) return;

        var filter = new Filter
        {
            Mode = mode,
            Start = ScheduleRange.Time(start),
            End = ScheduleRange.Time(end),
            Level = 50,
        };
        filter.ResetCurve();
        _settings.Filters.Add(filter);
    }

    static string NameOf(TintMode mode) => mode switch
    {
        TintMode.GoldenHour => "Golden hour",
        TintMode.Amber => "Amber",
        _ => "Astronomic",
    };
}
