using System.Windows;
using System.Windows.Controls;

namespace Strongthium.OutboundWatch.App;

/// <summary>The totals, read from the database on a background thread each time the range or the grouping changes.</summary>
public sealed partial class TotalsPage : UserControl, IPage
{
    private List<DestinationTotal> destinations = [];
    private List<GroupRow> rows = [];
    private string column = "Bytes";
    private bool descending = true;
    private int loading;

    public TotalsPage()
    {
        InitializeComponent();
        Ranges.Fill(Range, Ranges.Week);
        var start = Program.StartBy switch
        {
            "network" => ByOrganization,
            "country" => ByCountry,
            "domain" => ByDomain,
            "address" => ByAddress,
            "port" => ByPort,
            _ => ByProgram,
        };
        start.IsChecked = true;
    }

    private GroupBy By =>
        ByOrganization.IsChecked == true ? GroupBy.Organization
        : ByCountry.IsChecked == true ? GroupBy.Country
        : ByDomain.IsChecked == true ? GroupBy.Domain
        : ByAddress.IsChecked == true ? GroupBy.Address
        : ByPort.IsChecked == true ? GroupBy.Port
        : GroupBy.Program;

    public async void Refresh()
    {
        if (Range.SelectedIndex < 0) return;
        var version = ++loading;
        var app = App.Current;
        var now = DateTime.UtcNow;
        var from = Ranges.All[Range.SelectedIndex].From(now);
        CountText.Text = "Adding up…";
        var (found, traffic, first, step) = await Task.Run(() =>
        {
            app.Watcher.Save();
            var found = app.Database.Destinations(from, now.AddMinutes(1));
            // All that's kept starts at the first connection, not in 1970.
            var first = found.Count > 0 ? Later(found.Min(d => d.First), from) : from;
            var span = now - first;
            var step = span <= TimeSpan.FromHours(2) ? TimeSpan.FromMinutes(2)
                : span <= TimeSpan.FromDays(2) ? TimeSpan.FromHours(1)
                : TimeSpan.FromDays(1);
            first = Floor(first.ToLocalTime(), step).ToUniversalTime();
            return (found, app.Database.Traffic(first, now.AddMinutes(1), step), first, step);
        });
        if (version != loading) return;
        destinations = found;
        // Steps of a day start at local midnight; the database's are UTC: each is moved to the local day it's in.
        Chart.Show(step.TotalDays >= 1 ? ByLocalDay(traffic) : traffic, first, now, step);
        Group();
    }

    private static DateTime Later(DateTime first, DateTime from) => from > first ? from : first;

    private static DateTime Floor(DateTime local, TimeSpan step) =>
        step.TotalDays >= 1 ? local.Date : new DateTime(local.Ticks - local.Ticks % step.Ticks, local.Kind);

    private static List<(DateTime, long, long)> ByLocalDay(List<(DateTime At, long Sent, long Received)> traffic) =>
        [.. traffic.GroupBy(t => t.At.ToLocalTime().Date).Select(g => (g.Key.ToUniversalTime(), g.Sum(t => t.Sent), g.Sum(t => t.Received)))];

    private void Group()
    {
        var by = By;
        var groups = Totals.Group(destinations, by, App.Current.Lists);
        var largest = groups.Count > 0 ? groups.Max(g => g.Bytes) : 0;
        var longest = groups.Count > 0 ? groups.Max(g => g.Open) : TimeSpan.Zero;
        rows = [.. groups.Select(g => new GroupRow(g, by, largest, longest))];
        Show();
        var connections = groups.Sum(g => g.Connections);
        var unexpected = groups.Sum(g => g.Unexpected);
        var what = by switch
        {
            GroupBy.Program => "programs",
            GroupBy.Organization => "networks",
            GroupBy.Country => "countries",
            GroupBy.Domain => "domains",
            GroupBy.Address => "addresses",
            _ => "ports",
        };
        CountText.Text = $"{Sizes.Count(connections)} connections to {Sizes.Count(rows.Count)} {what}, {Ranges.All[Range.SelectedIndex].Label.ToLowerInvariant()}"
            + (unexpected > 0 ? $"; {Sizes.Count(unexpected)} unexpected" : "");
    }

    private void Show()
    {
        var order = GroupRow.Order(column);
        rows.Sort(descending ? (a, b) => order(b, a) : order);
        List.ItemsSource = null;
        List.ItemsSource = rows;
    }

    private void Range_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) Refresh();
    }

    private void By_Checked(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) Group();
    }

    private void Header_Click(object sender, RoutedEventArgs e)
    {
        (column, descending) = Actions.Sort(sender, column, descending, tag => tag != "Name");
        Show();
    }

    private void List_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (List.SelectedItem is not GroupRow) e.Handled = true;
    }

    private void List_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => History_Click(sender, e);

    private void History_Click(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is GroupRow row) this.MainWindowOf().ShowHistory(row.Name, row.Includes, Range.SelectedIndex);
    }

    private void Expect_Click(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is not GroupRow row) return;
        var dialog = new ExpectDialog(row.Group.Parts.OrderByDescending(p => p.Last).First().Latest, row.By) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            App.Current.ListsChanged();
            Group();
        }
    }
}
