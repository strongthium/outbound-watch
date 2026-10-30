using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Strongthium.OutboundWatch.App;

/// <summary>The times the history and the totals can show.</summary>
internal static class Ranges
{
    public static readonly (string Label, Func<DateTime, DateTime> From)[] All =
    [
        ("Last hour", now => now.AddHours(-1)),
        ("Today", _ => DateTime.Today.ToUniversalTime()),
        ("Last 24 hours", now => now.AddDays(-1)),
        ("Last 7 days", now => now.AddDays(-7)),
        ("Last 30 days", now => now.AddDays(-30)),
        ("All that's kept", _ => DateTime.UnixEpoch),
    ];

    public const int Week = 3;

    public static void Fill(ComboBox box, int selected)
    {
        foreach (var (label, _) in All) box.Items.Add(label);
        box.SelectedIndex = selected;
    }
}

/// <summary>The history, read from the database on a background thread each time what's asked for changes.</summary>
public sealed partial class HistoryPage : UserControl, IPage
{
    private const int Limit = 20_000;
    private readonly DispatcherTimer searchDelay = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private List<ConnectionRow> rows = [];
    private string column = "Started";
    private bool descending = true;
    private Func<Connection, bool>? only;
    private int loading;

    public HistoryPage()
    {
        InitializeComponent();
        Ranges.Fill(Range, 2);
        searchDelay.Tick += (_, _) =>
        {
            searchDelay.Stop();
            Refresh();
        };
    }

    /// <summary>Shows only the connections <paramref name="filter"/> picks, named <paramref name="label"/>.</summary>
    public void ShowOnly(string label, Func<Connection, bool> filter, int range)
    {
        only = filter;
        OnlyText.Text = "Only " + label;
        OnlyCard.Show(true);
        Search.Text = "";
        UnexpectedOnly.IsChecked = false;
        if (Range.SelectedIndex != range) Range.SelectedIndex = range;
        Refresh();
    }

    public async void Refresh()
    {
        if (Range.SelectedIndex < 0) return;
        var version = ++loading;
        var app = App.Current;
        var now = DateTime.UtcNow;
        var from = Ranges.All[Range.SelectedIndex].From(now);
        var search = Search.Text.Trim();
        var unexpectedOnly = UnexpectedOnly.IsChecked == true;
        var lists = app.Lists.ToList();
        var filter = only;
        CountText.Text = "Reading…";
        var (found, total) = await Task.Run(() =>
        {
            app.Watcher.Save();
            var connections = app.Database.Connections(from, now.AddMinutes(1), search, Limit);
            var list = new List<ConnectionRow>(connections.Count);
            foreach (var c in connections)
            {
                (c.Verdict, c.ExpectedBy) = ExpectedList.Judge(c, lists);
                if ((unexpectedOnly && c.Verdict != Verdict.Unexpected) || (filter is not null && !filter(c))) continue;
                list.Add(new ConnectionRow(c, now));
            }
            return (list, connections.Count);
        });
        if (version != loading) return;
        rows = found;
        Show();
        var range = Ranges.All[Range.SelectedIndex].Label.ToLowerInvariant();
        CountText.Text = (rows.Count == 1 ? "1 connection" : $"{Sizes.Count(rows.Count)} connections") + $", {range}"
            + (total >= Limit ? $" (the latest {Sizes.Count(Limit)})" : "");
    }

    private void Show()
    {
        var order = ConnectionRow.Order(column);
        rows.Sort(descending ? (a, b) => order(b, a) : order);
        List.ItemsSource = null;
        List.ItemsSource = rows;
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) Refresh();
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        searchDelay.Stop();
        searchDelay.Start();
    }

    private void ClearOnly_Click(object sender, RoutedEventArgs e)
    {
        only = null;
        OnlyCard.Show(false);
        Refresh();
    }

    private void Header_Click(object sender, RoutedEventArgs e)
    {
        (column, descending) = Actions.Sort(sender, column, descending, ConnectionRow.LargestFirst);
        Show();
    }

    private void List_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => Actions.History(List);
    private void List_ContextMenuOpening(object sender, ContextMenuEventArgs e) => Actions.Opening(List, e, CopyNameItem, ShowProgramItem);
    private void Expect_Click(object sender, RoutedEventArgs e) => Actions.Expect(List);
    private void History_Click(object sender, RoutedEventArgs e) => Actions.History(List);
    private void CopyAddress_Click(object sender, RoutedEventArgs e) => Actions.CopyAddress(List);
    private void CopyName_Click(object sender, RoutedEventArgs e) => Actions.CopyName(List);
    private void ShowProgram_Click(object sender, RoutedEventArgs e) => Actions.ShowProgram(List);
}
