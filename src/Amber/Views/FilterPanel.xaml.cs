using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Amber.Controls;
using Amber.Tint;

namespace Amber.Views;

/// <summary>
/// Settings of the selected filter: its mode, start and end, and either a flat level or a curve. Turning the curve on
/// replaces the slider with the graph; turning it off goes back to the flat level and keeps the curve for next time.
/// </summary>
public partial class FilterPanel : UserControl
{
    static readonly TintMode[] Modes = Enum.GetValues<TintMode>();
    const double RowHeight = 42, RowGap = 12, HueHeight = 22;

    Filter? _filter;
    bool _filling, _previewing;

    public FilterPanel()
    {
        InitializeComponent();

        StartField.ValueChanged += (_, _) => OnTimePicked(movingStart: true);
        EndField.ValueChanged += (_, _) => OnTimePicked(movingStart: false);

        // While the slider is dragged, the screen shows this filter's tint at that level, whatever the on/off state.
        Level.PreviewStarted += (_, _) =>
        {
            if (_filter is not { } filter) return;
            _previewing = true;
            App.Current.StartPreview(filter.Mode, Level.Value, filter.Hue);
        };
        Level.ValueChanged += (_, _) =>
        {
            if (_previewing && _filter is { } filter) App.Current.UpdatePreview(filter.Mode, Level.Value, filter.Hue);
        };
        Level.PreviewEnded += (_, _) => EndPreview();

        // Custom's colour bar the same way, at the level the filter shows now (its flat level, or its curve's level now).
        Hue.PreviewStarted += (_, _) =>
        {
            if (_filter is not { } filter) return;
            _previewing = true;
            App.Current.StartPreview(filter.Mode, LevelNow(filter), Hue.Value);
        };
        Hue.ValueChanged += (_, _) =>
        {
            if (_previewing && _filter is { } filter) App.Current.UpdatePreview(filter.Mode, LevelNow(filter), Hue.Value);
        };
        Hue.PreviewEnded += (_, _) => EndPreview();

        // Let go of the filter when the panel leaves the window, so a closed window can be freed.
        Unloaded += (_, _) => Filter = null;
    }

    void EndPreview()
    {
        if (!_previewing) return;
        _previewing = false;
        App.Current.EndPreview();
    }

    static double LevelNow(Filter filter) => AdvancedSchedule.LevelAt(filter, TimeOnly.FromDateTime(DateTime.Now));

    /// <summary>All the filters: a time can only move as far as the neighbours allow.</summary>
    public IReadOnlyList<Filter> Filters { get; set; } = [];

    /// <summary>The filter shown, or null. Switching filters shows the new one at once, without fades.</summary>
    public Filter? Filter
    {
        get => _filter;
        set
        {
            if (value == _filter) return;
            // The filter can change while its level or colour is being dragged (Esc, Delete): letting go of the slider
            // first ends the preview, so the slider never previews or moves a filter that has gone.
            if (_previewing)
            {
                Level.ReleaseMouseCapture();
                Hue.ReleaseMouseCapture();
            }
            if (_filter is not null) _filter.PropertyChanged -= OnFilterChanged;
            _filter = value;
            DataContext = value;
            Curve.Filter = null;
            if (_filter is null) return;

            _filter.PropertyChanged += OnFilterChanged;
            ShowMode();
            ShowTimes();
            ShowLevel();
            ShowCurve(animate: false);
        }
    }

    void OnFilterChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Amber.Filter.Mode):
                ShowMode();
                break;
            case nameof(Amber.Filter.Start):
            case nameof(Amber.Filter.End):
                ShowTimes();
                break;
            case nameof(Amber.Filter.Level):
                ShowLevel();
                break;
            case nameof(Amber.Filter.CurveEnabled):
                ShowCurve(animate: true);
                break;
        }
    }

    void ShowMode()
    {
        ModeField.Content = _filter!.Mode.DisplayName();
        ShowHue(animate: false);
    }

    /// <summary>
    /// Custom's colour bar: under the level slider, or in the slider's row while the curve is on. The panel grows by a
    /// row while it sits under the slider.
    /// </summary>
    void ShowHue(bool animate)
    {
        var custom = _filter!.Mode == TintMode.Custom;
        var inSliderRow = _filter.CurveEnabled;
        Hue.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetRow(Hue, inSliderRow ? 0 : 1);
        Hue.Margin = new Thickness(40, inSliderRow ? 0 : RowGap, 76, 0);
        Rows.Height = custom && !inSliderRow ? RowHeight + RowGap + HueHeight : RowHeight;
        if (custom) Fade(Hue, 0, 1, animate);
    }

    void ShowTimes()
    {
        _filling = true;
        StartField.Value = _filter!.Start;
        EndField.Value = _filter.End;
        _filling = false;
    }

    /// <summary>The number follows the slider live, also while dragging.</summary>
    void ShowLevel() => Percent.Text = _filter!.Level.ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>
    /// The curve takes the slider's place: the slider and number fade out and the graph fades in below (the window
    /// grows at once). Closing it hides the graph at once and brings the slider back.
    /// </summary>
    void ShowCurve(bool animate)
    {
        var show = _filter!.CurveEnabled;
        if (show && _filter.Curve.Count == 0) _filter.ResetCurve();
        Curve.Filter = show ? _filter : null;

        GraphCard.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show) Fade(GraphCard, 0, 1, animate);
        // Custom's colour bar moves up into the slider's row: the slider leaves at once rather than fading under it.
        var hueMovesUp = show && _filter.Mode == TintMode.Custom;
        Fade(LevelGroup, LevelGroup.Opacity, show ? 0 : 1, animate && !hueMovesUp);
        LevelGroup.IsHitTestVisible = !show;
        ShowHue(animate);
    }

    static void Fade(UIElement element, double from, double to, bool animate) =>
        element.BeginAnimation(OpacityProperty, animate
            ? new DoubleAnimation(from, to, new Duration(TimeSpan.FromSeconds(0.3)))
            : new DoubleAnimation(to, new Duration(TimeSpan.Zero)));

    /// <summary>
    /// A picked time moves that edge, stopping at the neighbouring filters just as a drag does;
    /// if it can't go all the way, the field snaps back to where the edge stopped.
    /// </summary>
    void OnTimePicked(bool movingStart)
    {
        if (_filling || _filter is null) return;

        FilterLayout.MoveEdge(_filter, Filters, movingStart, ScheduleRange.Minutes(movingStart ? StartField.Value : EndField.Value));
        ShowTimes();
    }

    /// <summary>The mode list: each mode with its dome in the mode colour.</summary>
    void OnModeClick(object sender, RoutedEventArgs e)
    {
        if (_filter is not { } filter) return;
        DropdownList.Open(ModeField, [.. Modes.Select(mode => ModeItem(this, mode))], -1, 2, index => filter.Mode = Modes[index]);
    }

    /// <summary>A mode in a menu, shared with the add menu.</summary>
    internal static Button ModeItem(FrameworkElement owner, TintMode mode) => new()
    {
        Style = (Style)owner.FindResource("ModeMenuItem"),
        Content = mode.DisplayName(),
        Background = (Brush)owner.FindResource(mode + "Accent"),
    };
}
