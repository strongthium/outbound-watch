using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Strongthium.OutboundWatch.App;

/// <summary>
/// The bytes received and sent over a time, a bar a step (an hour, or a day), received below in the accent colour and
/// sent above it, paler. The largest step is written at the top, and the times along the bottom.
/// </summary>
public sealed class TrafficChart : FrameworkElement
{
    private IReadOnlyList<(DateTime At, long Sent, long Received)> steps = [];
    private DateTime from, to;
    private TimeSpan step = TimeSpan.FromHours(1);

    public void Show(IReadOnlyList<(DateTime At, long Sent, long Received)> data, DateTime from, DateTime to, TimeSpan step)
    {
        steps = data;
        this.from = from;
        this.to = to;
        this.step = step;
        InvalidateVisual();
    }

    private Brush Find(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

    protected override void OnRender(DrawingContext dc)
    {
        var text = Find("TextFillColorSecondaryBrush", Brushes.Gray);
        var accent = Find("AccentFillColorDefaultBrush", Brushes.SteelBlue);
        var grid = Find("DividerStrokeColorDefaultBrush", Brushes.LightGray);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        FormattedText Label(string s) => new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, text, dpi);

        const double bottom = 18, top = 16;
        var width = ActualWidth;
        var height = ActualHeight - bottom - top;
        if (width <= 0 || height <= 0 || to <= from) return;
        var count = Math.Max(1, (int)Math.Ceiling((to - from) / step));
        var slot = width / count;
        var largest = steps.Count == 0 ? 0 : steps.Max(s => s.Sent + s.Received);

        dc.DrawLine(new Pen(grid, 1), new Point(0, top + height + 0.5), new Point(width, top + height + 0.5));
        if (largest == 0)
        {
            var none = Label("No bytes counted in this time (they're counted when it runs as administrator).");
            dc.DrawText(none, new Point((width - none.Width) / 2, top + height / 2 - none.Height / 2));
        }
        else
        {
            dc.DrawText(Label(Sizes.Format(largest) + (step.TotalHours >= 24 ? " a day" : step.TotalHours >= 1 ? " an hour" : " a minute")), new Point(0, 0));
            var sentBrush = accent.Clone();
            sentBrush.Opacity = 0.45;
            var gap = slot > 6 ? Math.Min(3, slot * 0.2) : 0;
            foreach (var (at, sent, received) in steps)
            {
                var index = (int)((at - from) / step);
                if (index < 0 || index >= count) continue;
                var x = index * slot + gap / 2;
                var w = Math.Max(1, slot - gap);
                var r = height * received / largest;
                var s = height * sent / largest;
                dc.DrawRectangle(accent, null, new Rect(x, top + height - r, w, r));
                dc.DrawRectangle(sentBrush, null, new Rect(x, top + height - r - s, w, s));
            }
        }

        // Times along the bottom: the start, the middle and the end.
        var format = step.TotalHours >= 24 || to - from > TimeSpan.FromDays(2) ? "MMM d" : "HH:mm";
        foreach (var (at, align) in new[] { (from, 0.0), (from + (to - from) / 2, 0.5), (to, 1.0) })
        {
            var label = Label(at.ToLocalTime().ToString(format, CultureInfo.CurrentCulture));
            dc.DrawText(label, new Point(Math.Clamp(width * align - label.Width * align, 0, width - label.Width), top + height + 3));
        }
    }
}
