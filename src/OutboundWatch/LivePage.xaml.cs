using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Strongthium.OutboundWatch.App;

/// <summary>The connections open now, read from the watcher every second while the page shows.</summary>
public sealed partial class LivePage : UserControl, IPage
{
    private readonly ObservableCollection<ConnectionRow> rows = [];
    private readonly Dictionary<FlowKey, ConnectionRow> byKey = [];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string column = "Started";
    private bool descending = true;
    private int ticks;

    public LivePage()
    {
        InitializeComponent();
        List.ItemsSource = rows;
        timer.Tick += (_, _) => Tick();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                Tick();
                timer.Start();
            }
            else timer.Stop();
        };
    }

    public void Refresh()
    {
        ShowPlacesCard();
        ticks = 0;
        Tick();
    }

    public void ShowUnexpected()
    {
        Search.Text = "";
        UnexpectedOnly.IsChecked = true;
    }

    private void Tick()
    {
        var app = App.Current;
        var now = DateTime.UtcNow;
        var open = app.Watcher.Snapshot();
        var search = Search.Text.Trim();
        var unexpectedOnly = UnexpectedOnly.IsChecked == true;

        var shown = new HashSet<FlowKey>();
        foreach (var c in open)
        {
            if (byKey.TryGetValue(c.Key, out var row)) row.Update(c, now);
            else row = new ConnectionRow(c, now);
            if ((unexpectedOnly && c.Verdict != Verdict.Unexpected) || !row.Contains(search)) continue;
            shown.Add(c.Key);
            if (byKey.TryAdd(c.Key, row)) rows.Add(row);
        }
        foreach (var gone in byKey.Keys.Where(key => !shown.Contains(key)).ToList())
        {
            rows.Remove(byKey[gone]);
            byKey.Remove(gone);
        }
        var order = ConnectionRow.Order(column);
        Rows.SortInPlace(rows, descending ? (a, b) => order(b, a) : order);

        var unexpected = open.Count(c => c.Verdict == Verdict.Unexpected);
        var checking = app.Lists.Any(l => l.Enabled);
        CountText.Text = rows.Count == open.Count
            ? $"What's connected now. Watching since {Rows.When(app.Started)}."
            : $"{Sizes.Count(rows.Count)} of {Sizes.Count(open.Count)} open connections";
        OpenStat.Text = Sizes.Count(open.Count);
        UnexpectedStat.Text = checking ? Sizes.Count(unexpected) : "—";
        UnexpectedCaption.Text = checking ? "not on an expected list" : "no expected list is on";
        var countries = open.Select(c => c.Place.Country).Where(c => c.Length > 0).Distinct().Count();
        var networks = open.Select(c => c.Place.Asn).Where(a => a > 0).Distinct().Count();
        PlacesStat.Text = $"{networks} / {countries}";
        PlacesCaption.Text = "networks / countries";

        // Today's bytes, from the history, every few seconds.
        if (ticks++ % 5 == 0)
        {
            if (app.Watcher.Measuring)
            {
                var today = DateTime.Today.ToUniversalTime();
                var traffic = app.Database.Traffic(today, now.AddMinutes(1), TimeSpan.FromDays(1));
                ReceivedStat.Text = Sizes.Format(traffic.Sum(t => t.Received));
                SentStat.Text = Sizes.Format(traffic.Sum(t => t.Sent));
            }
            else ReceivedStat.Text = SentStat.Text = "—";
            ShowPlacesCard();
        }
    }

    private void ShowPlacesCard()
    {
        var app = App.Current;
        PlacesCard.Show(!Program.Sample && (app.Places is null || app.LoadingPlaces));
        DownloadButton.Show(!app.LoadingPlaces);
        if (!DownloadButton.IsEnabled) return;
        PlacesText.Text = app.LoadingPlaces
            ? "Reading the address databases…"
            : "To see each address's network and country, download DB-IP's free address databases: about 12 MB, kept on this PC and updated monthly. After that, nothing is looked up online.";
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        DownloadButton.IsEnabled = false;
        try
        {
            await App.Current.DownloadPlacesAsync(new Progress<string>(text => PlacesText.Text = text), CancellationToken.None);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or InvalidDataException or TaskCanceledException)
        {
            MessageBox.Show(Window.GetWindow(this), "The address databases couldn't be downloaded: " + exception.Message, "outbound-watch", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            DownloadButton.IsEnabled = true;
            ShowPlacesCard();
        }
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) Tick();
    }

    private void Header_Click(object sender, RoutedEventArgs e)
    {
        (column, descending) = Actions.Sort(sender, column, descending, ConnectionRow.LargestFirst);
        Tick();
    }

    private void List_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => Actions.History(List);
    private void List_ContextMenuOpening(object sender, ContextMenuEventArgs e) => Actions.Opening(List, e, CopyNameItem, ShowProgramItem);
    private void Expect_Click(object sender, RoutedEventArgs e) => Actions.Expect(List);
    private void History_Click(object sender, RoutedEventArgs e) => Actions.History(List);
    private void CopyAddress_Click(object sender, RoutedEventArgs e) => Actions.CopyAddress(List);
    private void CopyName_Click(object sender, RoutedEventArgs e) => Actions.CopyName(List);
    private void ShowProgram_Click(object sender, RoutedEventArgs e) => Actions.ShowProgram(List);
}
