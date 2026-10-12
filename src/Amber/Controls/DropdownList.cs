using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Amber.Controls;

/// <summary>The white list that drops down under a field: hours, minutes or transition lengths.</summary>
static class DropdownList
{
    const double ItemHeight = 40, Padding = 6, MaxHeight = 240, Gap = 6;
    const double ShadowRoom = 28; // transparent room around the menu, so its shadow isn't cut off

    /// <summary>
    /// Opens a list centred under <paramref name="field"/> (or over it, when there is no room below), with the current
    /// choice marked and scrolled into view; the field's text takes the open-dropdown colour meanwhile. Picking an item
    /// closes the list and calls <paramref name="choose"/> with its index; Esc or a click elsewhere just closes it.
    /// </summary>
    public static void Open(Control field, IReadOnlyList<string> choices, int current, double itemWidth, Action<int> choose)
    {
        var visible = Math.Min(choices.Count * ItemHeight, MaxHeight - 2 * Padding);
        var popup = new Popup
        {
            PlacementTarget = field,
            Placement = PlacementMode.Custom,
            CustomPopupPlacementCallback = (_, fieldSize, _) => Placements(field, fieldSize, itemWidth + 2 * Padding, visible + 2 * Padding),
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
        };

        var items = new StackPanel();
        for (var i = 0; i < choices.Count; i++)
        {
            var index = i;
            var item = new Button
            {
                Content = choices[i],
                Width = itemWidth,
                Style = (Style)field.FindResource("MenuItem"),
                Tag = i == current ? "Current" : null,
            };
            item.Click += (_, _) =>
            {
                popup.IsOpen = false;
                choose(index);
            };
            items.Children.Add(item);
        }

        var scroller = new ScrollViewer { Content = items, MaxHeight = visible, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
        var menu = new Border { Style = (Style)field.FindResource("Menu"), Margin = new Thickness(ShadowRoom), Child = scroller };
        // A popup doesn't inherit the window's text look, so copy it.
        TextOptions.SetTextRenderingMode(menu, TextOptions.GetTextRenderingMode(field));
        TextOptions.SetTextHintingMode(menu, TextOptions.GetTextHintingMode(field));
        popup.Child = menu;

        // Show the current choice in the middle of the list when it is long enough to scroll.
        popup.Opened += (_, _) =>
        {
            scroller.UpdateLayout();
            scroller.ScrollToVerticalOffset(Math.Clamp(current * ItemHeight - (visible - ItemHeight) / 2, 0, choices.Count * ItemHeight - visible));
        };
        popup.Closed += (_, _) => field.ClearValue(Control.ForegroundProperty);

        // The keyboard stays with the window while the list is open, so Esc is caught there.
        if (Window.GetWindow(field) is { } window)
        {
            KeyEventHandler closeOnEscape = (_, e) =>
            {
                if (e.Key != Key.Escape) return;
                popup.IsOpen = false;
                e.Handled = true;
            };
            window.PreviewKeyDown += closeOnEscape;
            popup.Closed += (_, _) => window.PreviewKeyDown -= closeOnEscape;
        }

        field.Foreground = (Brush)field.FindResource("OpenTextBrush");
        popup.IsOpen = true;
    }

    /// <summary>
    /// Where the list may go, in order: 6 px below the field, or 6 px above it when the screen has no room below;
    /// centred on the field either way. The points place the popup, so its shadow margin comes off.
    /// </summary>
    static CustomPopupPlacement[] Placements(Control field, Size fieldSize, double menuWidth, double menuHeight)
    {
        // WPF may hand over screen pixels; the field's own size is in layout units, so the ratio converts.
        var scale = fieldSize.Width / field.ActualWidth;
        var left = (field.ActualWidth - menuWidth) / 2 - ShadowRoom;
        return
        [
            new CustomPopupPlacement(new Point(left * scale, (field.ActualHeight + Gap - ShadowRoom) * scale), PopupPrimaryAxis.Horizontal),
            new CustomPopupPlacement(new Point(left * scale, (-Gap - menuHeight - ShadowRoom) * scale), PopupPrimaryAxis.Horizontal),
        ];
    }
}
