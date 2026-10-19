using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Amber.Controls;
using Amber.Tint;

namespace Amber.Views;

/// <summary>
/// Advanced mode: a timeline of filters. With nothing selected, "Add filter" sits below it; selecting a bar shows its
/// settings there instead. Esc deselects and Delete removes the selected filter.
/// </summary>
public partial class AdvancedView : UserControl
{
    static readonly TintMode[] Modes = Enum.GetValues<TintMode>();

    readonly Settings _settings;
    HintBubble? _hint;
    AdornerLayer? _hintLayer;
    Window? _window;

    public AdvancedView(Settings settings, Updates updates)
    {
        InitializeComponent();
        _settings = settings;
        DataContext = settings;
        Notice.Updates = updates;
        Timeline.Filters = settings.Filters;
        Panel.Filters = settings.Filters;

        Timeline.SelectedChanged += (_, _) => ShowSelection();
        Timeline.DeleteRequested += (_, filter) => Delete(filter);
        ShowIntro();
        ShowSelection();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += (_, _) => ShowHint();
    }

    /// <summary>
    /// Follows the settings while the view is in the window. The hint floats over the whole window, and the keys work
    /// wherever the focus is, so both hook into the window.
    /// </summary>
    void OnLoaded(object sender, RoutedEventArgs e)
    {
        _settings.PropertyChanged += OnSettingChanged;
        ShowIntro();

        _window = Window.GetWindow(this);
        if (_window is null) return;
        _window.KeyDown += OnWindowKeyDown;

        // The window's own layer: the one nearest the timeline belongs to the scrolling body and would cut the bubble off.
        _hintLayer = AdornerLayer.GetAdornerLayer((Visual)_window.Content);
        _hint = new HintBubble(Timeline, "Click a filter", "to adjust it.");
        _hintLayer?.Add(_hint);
        ShowHint();
    }

    /// <summary>Lets go of the settings, the window and the hint, so a closed window can be freed.</summary>
    void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _settings.PropertyChanged -= OnSettingChanged;
        if (_window is not null) _window.KeyDown -= OnWindowKeyDown;
        if (_hint is not null) _hintLayer?.Remove(_hint);
        (_window, _hint, _hintLayer) = (null, null, null);
    }

    void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Settings.Filters)) return;
        ShowIntro();
        ShowHint();
    }

    /// <summary>"How it works" explains the mode while there are no filters, until it is closed.</summary>
    void ShowIntro() =>
        Intro.Visibility = _settings.Filters.Count == 0 && !_settings.AdvancedIntroClosed ? Visibility.Visible : Visibility.Collapsed;

    void OnCloseIntro(object sender, RoutedEventArgs e)
    {
        _settings.AdvancedIntroClosed = true;
        ShowIntro();
    }

    /// <summary>A selected filter shows its settings in place of "Add filter". Selecting one also retires the hint.</summary>
    void ShowSelection()
    {
        var selected = Timeline.Selected;
        Panel.Filter = selected;
        Panel.Visibility = selected is null ? Visibility.Collapsed : Visibility.Visible;
        AddButton.Visibility = selected is null ? Visibility.Visible : Visibility.Collapsed;
        if (selected is not null) _settings.FilterHintDone = true;
        ShowHint();
    }

    /// <summary>
    /// "Click a filter to adjust it" points at the first filter from the moment there is one, until a filter is selected
    /// for the first time. It hides while Settings or About cover the window.
    /// </summary>
    void ShowHint()
    {
        if (_hint is null) return;
        var show = IsVisible && !_settings.FilterHintDone && _settings.Filters.Count > 0;
        _hint.Tip = show ? Timeline.HintTarget(_settings.Filters[0]) : null;
    }

    IEnumerable<(int Start, int End)> Ranges() => _settings.Filters.Select(filter => filter.Range);

    /// <summary>The add menu: the four tints, with a + on hover; all faded when there is no free stretch of 15 minutes left.</summary>
    void OnAddClick(object sender, RoutedEventArgs e)
    {
        var room = FilterLayout.HasRoom(Ranges());
        var items = Modes.Select(mode =>
        {
            var item = FilterPanel.ModeItem(this, mode);
            item.Tag = "Add";
            item.IsEnabled = room;
            return item;
        }).ToArray();
        DropdownList.Open(AddButton, items, -1, 2, index => Add(Modes[index]));
    }

    /// <summary>A new filter takes the left-most free hour, or whatever is left of it. It isn't selected: the hint points at it instead.</summary>
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

    void Delete(Filter filter)
    {
        if (Timeline.Selected == filter) Timeline.Selected = null;
        _settings.Filters.Remove(filter);
    }

    /// <summary>Esc deselects; Delete removes the selected filter (handy when its bar is too narrow for the icon). Not while hidden.</summary>
    void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsVisible || Timeline.Selected is not { } selected) return;
        if (e.Key == Key.Escape) Timeline.Selected = null;
        else if (e.Key == Key.Delete) Delete(selected);
        else return;
        e.Handled = true;
    }
}
