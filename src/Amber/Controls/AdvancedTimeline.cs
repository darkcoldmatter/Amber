using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Media;
using Amber.Tint;

namespace Amber.Controls;

/// <summary>The bar under the pointer (-1 for none) and, when the pointer is on one of its edges, which: true = start, false = end.</summary>
public readonly record struct BarHit(int Index, bool? Edge)
{
    public static readonly BarHit None = new(-1, null);
}

/// <summary>
/// Advanced mode's 24-hour timeline (Figma 111:4378): hour labels over an amber track with a white bar per filter, each
/// showing its mode's dome over its level, or a curve mark. Clicking a bar selects it: it stays white and shows a delete
/// icon while the others fade. Either edge of a bar can be dragged; it snaps to 15 minutes, wraps past midnight and stops
/// at the neighbouring filters. Nothing on the timeline animates.
/// </summary>
public class AdvancedTimeline : FrameworkElement
{
    const double TrackTop = 32, TrackHeight = 80, TrackRadius = 14, LabelsAbove = 15.5;
    // The bars sit 2 px inside the track, so the day runs from x 2 to the width - 2; touching bars share their border.
    const double Inset = 2, BarRadius = 12, OutlineWidth = 1.5;
    // Inside a bar: the dome's top, the middle of the level text (or curve mark), and the room kept at the sides.
    const double DomeTop = 25, DomeWidth = 15, ContentMiddle = 46, ContentRoom = 4;
    // The delete icon on the selected bar: 16 px, 6 px below its top and 6 px in from its right; it needs 28 px of bar.
    const double RemoveSize = 16, RemoveInset = 6, RemoveRoom = RemoveSize + 2 * RemoveInset;
    const double HintGap = 6; // between the hint bubble's tip and the track

    static readonly Brush TrackBrush = Timeline.Frozen(new SolidColorBrush(Color.FromRgb(0xEE, 0xC3, 0x7D)));
    static readonly Brush FadedBarBrush = Timeline.Frozen(new SolidColorBrush(Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF)));
    static readonly Brush InkBrush = Timeline.Frozen(new SolidColorBrush(Color.FromRgb(0x27, 0x1F, 0x1F)));
    static readonly Pen BorderPen = Timeline.Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0xED, 0xC6, 0x87)), 1));
    static readonly Pen OutlinePen = Timeline.Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0x4D, 0x27, 0x1F, 0x1F)), OutlineWidth));
    static readonly Pen RemovePen = Timeline.Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0x7B, 0x7D, 0x87)), 1.5));
    static readonly Pen RemoveHoverPen = Timeline.Frozen(new Pen(InkBrush, 1.5));

    ObservableCollection<Filter>? _filters;
    readonly List<Filter> _watched = [];
    bool _attached;
    Filter? _selected, _hovered;
    bool _removeHovered;

    // While the left button is down: the bar pressed (null for the empty track), the edge being dragged, whether the
    // press became a drag (then it isn't a click), and whether it was on the delete icon.
    Filter? _pressed;
    bool? _dragEdge;
    bool _dragged, _pressedRemove;
    Point _pressPoint;

    public AdvancedTimeline()
    {
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    /// <summary>The filters to show. The timeline follows changes to the list and to every filter in it while it is loaded.</summary>
    public ObservableCollection<Filter>? Filters
    {
        get => _filters;
        set
        {
            Detach();
            _filters = value;
            if (IsLoaded) Attach();
            InvalidateVisual();
        }
    }

    /// <summary>The selected filter, or null. Clicks change it, and so can the view (Esc, Delete).</summary>
    public Filter? Selected
    {
        get => _selected;
        set
        {
            if (value == _selected) return;
            _selected = value;
            InvalidateVisual();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? SelectedChanged;

    /// <summary>The delete icon on the selected bar was clicked.</summary>
    public event EventHandler<Filter>? DeleteRequested;

    /// <summary>
    /// What the pointer at <paramref name="x"/> is on: the bar it is inside, or else a bar whose edge is within reach
    /// (the nearer one, if two are). Bars are grabbed by their own edges from inside, so where two bars touch, each side
    /// of the line moves the bar the pointer is in.
    /// </summary>
    public static BarHit HitTest(double x, IReadOnlyList<(int Start, int End)> bars, double width)
    {
        for (var i = 0; i < bars.Count; i++)
            if (Timeline.Contains(x, bars[i].Start, bars[i].End, width))
                return new BarHit(i, Timeline.EdgeAt(x, bars[i].Start, bars[i].End, width));

        var nearest = BarHit.None;
        var nearestDistance = double.MaxValue;
        for (var i = 0; i < bars.Count; i++)
        {
            if (Timeline.EdgeAt(x, bars[i].Start, bars[i].End, width) is not bool isStart) continue;
            var distance = Math.Abs(x - (isStart ? Timeline.X(bars[i].Start, width) : Timeline.EndX(bars[i].End, width)));
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearest = new BarHit(i, isStart);
        }
        return nearest;
    }

    /// <summary>Where the hint bubble points: just above the middle of the filter's bar (its wider piece, past midnight).</summary>
    public Point HintTarget(Filter filter)
    {
        var bar = Widest(filter);
        return new Point(bar.Left + bar.Width / 2, TrackTop - HintGap);
    }

    void Attach()
    {
        if (_attached || _filters is null) return;
        _attached = true;
        _filters.CollectionChanged += OnFiltersChanged;
        Watch();
    }

    /// <summary>Lets go of the filters when the timeline leaves the window, so a closed window can be freed.</summary>
    void Detach()
    {
        if (!_attached || _filters is null) return;
        _attached = false;
        _filters.CollectionChanged -= OnFiltersChanged;
        Unwatch();
    }

    void Watch()
    {
        foreach (var filter in _filters!)
        {
            filter.PropertyChanged += OnFilterChanged;
            _watched.Add(filter);
        }
    }

    void Unwatch()
    {
        foreach (var filter in _watched) filter.PropertyChanged -= OnFilterChanged;
        _watched.Clear();
    }

    void OnFiltersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Unwatch();
        Watch();
        if (_hovered is not null && !_filters!.Contains(_hovered)) _hovered = null;
        if (_selected is not null && !_filters!.Contains(_selected)) Selected = null;
        UIElementAutomationPeer.FromElement(this)?.ResetChildrenCache();
        InvalidateVisual();
    }

    void OnFilterChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    double DayWidth => Math.Max(ActualWidth - 2 * Inset, 1);

    static (int Start, int End) RangeOf(Filter filter) => (ScheduleRange.Minutes(filter.Start), ScheduleRange.Minutes(filter.End));

    /// <summary>A filter's bar: one piece, or two past midnight; on whole pixels, so the edges stay sharp.</summary>
    IEnumerable<Rect> Pieces(Filter filter)
    {
        var (start, end) = RangeOf(filter);
        foreach (var (left, right) in Timeline.Pieces(start, end, DayWidth))
        {
            var x0 = Math.Round(Inset + left);
            var x1 = Math.Round(Inset + right);
            yield return new Rect(x0, TrackTop + Inset, Math.Max(x1 - x0, 1), TrackHeight - 2 * Inset);
        }
    }

    Rect Widest(Filter filter) => Pieces(filter).MaxBy(piece => piece.Width);

    /// <summary>The delete icon's box at the top right of the selected bar, or empty when there is none or no room.</summary>
    Rect RemoveBox()
    {
        if (_selected is null) return Rect.Empty;
        var bar = Widest(_selected);
        return bar.Width < RemoveRoom ? Rect.Empty
            : new Rect(bar.Right - RemoveInset - RemoveSize, bar.Top + RemoveInset, RemoveSize, RemoveSize);
    }

    static bool OnTrack(Point point) => point.Y >= TrackTop && point.Y <= TrackTop + TrackHeight;

    BarHit HitAt(Point point) => _filters is null || !OnTrack(point)
        ? BarHit.None
        : HitTest(point.X - Inset, _filters.Select(RangeOf).ToArray(), DayWidth);

    Filter? FilterOf(BarHit hit) => hit.Index >= 0 ? _filters![hit.Index] : null;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var point = e.GetPosition(this);
        if (!OnTrack(point) || !CaptureMouse()) return; // without the mouse, a drag could never end
        e.Handled = true;
        Press(point);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsMouseCaptured) DragTo(e.GetPosition(this));
        else ShowHover(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!IsMouseCaptured) return;
        Release(e.GetPosition(this));
        ReleaseMouseCapture();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        (_pressed, _dragEdge, _dragged, _pressedRemove) = (null, null, false, false);
        ShowHover(e.GetPosition(this));
    }

    void Press(Point point)
    {
        var hit = HitAt(point);
        _pressPoint = point;
        _pressed = FilterOf(hit);
        _pressedRemove = RemoveBox().Contains(point);
        _dragEdge = _pressedRemove ? null : hit.Edge;
        _dragged = false;
    }

    /// <summary>A press on a bar's edge drags that edge; moving the pointer more than a nudge makes it a drag rather than a click.</summary>
    void DragTo(Point point)
    {
        if (_pressed is not { } filter || _dragEdge is not bool movingStart) return;

        if (Math.Abs(point.X - _pressPoint.X) >= SystemParameters.MinimumHorizontalDragDistance) _dragged = true;
        var before = RangeOf(filter);
        var others = _filters!.Where(other => other != filter).Select(RangeOf);
        var (start, end) = FilterLayout.MoveEdge(before, others, movingStart, ScheduleRange.MinutesAt(point.X - Inset, DayWidth));
        if ((start, end) == before) return;
        filter.Start = ScheduleRange.Time(start);
        filter.End = ScheduleRange.Time(end);
        _dragged = true;
    }

    /// <summary>
    /// A click (press and release on the same bar, without dragging an edge) selects that bar, or deselects it if it was
    /// selected already; a click on the empty track deselects. A click on the delete icon asks for the filter to go.
    /// </summary>
    void Release(Point point)
    {
        var (pressed, pressedRemove, dragged) = (_pressed, _pressedRemove, _dragged);
        (_pressed, _dragEdge, _dragged, _pressedRemove) = (null, null, false, false);

        if (pressedRemove)
        {
            if (RemoveBox().Contains(point) && _selected is { } selected) DeleteRequested?.Invoke(this, selected);
            return;
        }
        if (dragged || FilterOf(HitAt(point)) != pressed) return;
        Selected = pressed == _selected ? null : pressed;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (!IsMouseCaptured) SetHover(null, false);
    }

    /// <summary>The double arrow shows only on a bar's own edge; hovering a bar (or its edge) outlines it at once.</summary>
    void ShowHover(Point point)
    {
        var hit = HitAt(point);
        var onRemove = RemoveBox().Contains(point);
        Cursor = hit.Edge is null || onRemove ? Cursors.Arrow : Timeline.ResizeCursor;
        SetHover(FilterOf(hit), onRemove);
    }

    void SetHover(Filter? filter, bool onRemove)
    {
        if (filter == _hovered && onRemove == _removeHovered) return;
        (_hovered, _removeHovered) = (filter, onRemove);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var day = new Rect(Inset, TrackTop, DayWidth, TrackHeight);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var font = (FontFamily)FindResource("NumberFont");
        Timeline.DrawHourLabels(dc, day, TrackTop - LabelsAbove, font, dpi);
        dc.DrawRoundedRectangle(TrackBrush, null, new Rect(0, TrackTop, ActualWidth, TrackHeight), TrackRadius, TrackRadius);
        Timeline.DrawHourGrid(dc, day);
        if (_filters is null || _filters.Count == 0) return;

        // All the fills first, then every border in one stroke, so two bars that touch share a single line.
        var borders = new GeometryGroup();
        foreach (var filter in _filters)
            foreach (var piece in Pieces(filter))
            {
                var fill = _selected is null || filter == _selected ? Brushes.White : FadedBarBrush;
                dc.DrawRoundedRectangle(fill, null, piece, BarRadius, BarRadius);
                borders.Children.Add(new RectangleGeometry(piece, BarRadius, BarRadius));
            }
        dc.DrawGeometry(null, BorderPen, borders);

        var typeface = new Typeface(font, FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
        foreach (var filter in _filters) DrawContent(dc, filter, Widest(filter), typeface, dpi);
        DrawRemove(dc);
        if (_hovered is not null) DrawOutline(dc, Pieces(_hovered));
    }

    /// <summary>The dome in the bar colour over the level ("70%") or the curve mark, centred; left out when the bar is too narrow.</summary>
    void DrawContent(DrawingContext dc, Filter filter, Rect bar, Typeface typeface, double dpi)
    {
        var level = filter.CurveEnabled ? null : new FormattedText(filter.Level.ToString("0", CultureInfo.InvariantCulture) + "%",
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12, InkBrush, dpi);
        // Measured by its ink, so "50%" still shows on a new one-hour bar.
        var levelWidth = level?.BuildGeometry(default).Bounds.Width ?? 0;
        if (bar.Width < Math.Max(DomeWidth, levelWidth) + 2 * ContentRoom) return;

        var middle = bar.Left + bar.Width / 2;
        dc.PushTransform(new TranslateTransform(Math.Floor(middle - DomeWidth / 2), bar.Top + DomeTop));
        dc.DrawGeometry((Brush)FindResource(filter.Mode + "Bar"), null, (Geometry)FindResource("IconDomeBar"));
        dc.Pop();

        if (level is not null)
        {
            // Centre the digits themselves (cap height to baseline), like Figma's trimmed text.
            var capHeight = typeface.TryGetGlyphTypeface(out var glyphs) ? glyphs.CapsHeight * 12 : 0;
            dc.DrawText(level, new Point(middle - level.Width / 2, bar.Top + ContentMiddle + capHeight / 2 - level.Baseline));
            return;
        }
        var curve = (Geometry)FindResource("IconBarCurve");
        dc.PushTransform(new TranslateTransform(Math.Floor(middle - curve.Bounds.Width / 2), bar.Top + ContentMiddle - curve.Bounds.Height / 2));
        dc.DrawGeometry(InkBrush, null, curve);
        dc.Pop();
    }

    void DrawRemove(DrawingContext dc)
    {
        var box = RemoveBox();
        if (box.IsEmpty) return;
        dc.PushTransform(new TranslateTransform(box.X, box.Y));
        dc.DrawGeometry(null, _removeHovered ? RemoveHoverPen : RemovePen, (Geometry)FindResource("IconRemove16"));
        dc.Pop();
    }

    /// <summary>The hover outline sits inside the bar, like a Figma inside stroke.</summary>
    static void DrawOutline(DrawingContext dc, IEnumerable<Rect> pieces)
    {
        foreach (var piece in pieces)
        {
            if (piece.Width <= OutlineWidth) continue;
            var outline = piece;
            outline.Inflate(-OutlineWidth / 2, -OutlineWidth / 2);
            dc.DrawRoundedRectangle(null, OutlinePen, outline, BarRadius - OutlineWidth / 2, BarRadius - OutlineWidth / 2);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TimelinePeer(this);

    /// <summary>
    /// Lists the filters to screen readers and UI Automation as items that can be selected, which does what a click does.
    /// </summary>
    sealed class TimelinePeer(AdvancedTimeline owner) : FrameworkElementAutomationPeer(owner), ISelectionProvider
    {
        readonly Dictionary<Filter, BarPeer> _bars = [];

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
        protected override string GetClassNameCore() => nameof(AdvancedTimeline);

        protected override List<AutomationPeer> GetChildrenCore()
        {
            var filters = owner.Filters ?? [];
            foreach (var gone in _bars.Keys.Where(filter => !filters.Contains(filter)).ToArray()) _bars.Remove(gone);
            return [.. filters.Select(filter => (AutomationPeer)(_bars.TryGetValue(filter, out var bar) ? bar : _bars[filter] = new BarPeer(owner, filter)))];
        }

        public override object GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Selection ? this : base.GetPattern(patternInterface);

        bool ISelectionProvider.CanSelectMultiple => false;
        bool ISelectionProvider.IsSelectionRequired => false;

        IRawElementProviderSimple[] ISelectionProvider.GetSelection() =>
            owner.Selected is { } selected && _bars.TryGetValue(selected, out var bar) ? [ProviderFromPeer(bar)] : [];
    }

    sealed class BarPeer(AdvancedTimeline owner, Filter filter) : AutomationPeer, ISelectionItemProvider
    {
        protected override string GetNameCore() =>
            $"{filter.Mode.DisplayName()}, {filter.Start:HH\\:mm} to {filter.End:HH\\:mm}";

        protected override Rect GetBoundingRectangleCore()
        {
            if (PresentationSource.FromVisual(owner) is null) return Rect.Empty;
            var bar = owner.Widest(filter);
            return new Rect(owner.PointToScreen(bar.TopLeft), owner.PointToScreen(bar.BottomRight));
        }

        protected override Point GetClickablePointCore()
        {
            var bounds = GetBoundingRectangleCore();
            return bounds.IsEmpty ? new Point(double.NaN, double.NaN) : new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        }

        public override object GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.SelectionItem ? this : null!;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
        protected override List<AutomationPeer> GetChildrenCore() => [];
        protected override string GetClassNameCore() => "FilterBar";
        protected override bool IsOffscreenCore() => !owner.IsVisible;
        protected override bool IsEnabledCore() => true;
        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;
        protected override string GetAcceleratorKeyCore() => "";
        protected override string GetAccessKeyCore() => "";
        protected override string GetAutomationIdCore() => "";
        protected override string GetHelpTextCore() => "";
        protected override string GetItemStatusCore() => "";
        protected override string GetItemTypeCore() => "";
        protected override AutomationPeer GetLabeledByCore() => null!;
        protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;
        protected override bool HasKeyboardFocusCore() => false;
        protected override bool IsKeyboardFocusableCore() => false;
        protected override bool IsPasswordCore() => false;
        protected override bool IsRequiredForFormCore() => false;
        protected override void SetFocusCore() { }

        bool ISelectionItemProvider.IsSelected => owner.Selected == filter;

        IRawElementProviderSimple ISelectionItemProvider.SelectionContainer =>
            ProviderFromPeer(UIElementAutomationPeer.CreatePeerForElement(owner));

        void ISelectionItemProvider.Select() => owner.Selected = filter;
        void ISelectionItemProvider.AddToSelection() => owner.Selected = filter;

        void ISelectionItemProvider.RemoveFromSelection()
        {
            if (owner.Selected == filter) owner.Selected = null;
        }
    }
}
