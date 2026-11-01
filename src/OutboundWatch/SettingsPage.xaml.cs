using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Strongthium.OutboundWatch.App;

public sealed partial class SettingsPage : UserControl, IPage
{
    private static readonly (string Label, int Days)[] KeepChoices = [("7 days", 7), ("30 days", 30), ("90 days", 90), ("A year", 365), ("Everything", 0)];
    private bool showing;

    public SettingsPage()
    {
        InitializeComponent();
        foreach (var (label, _) in KeepChoices) Keep.Items.Add(label);
        var version = typeof(App).Assembly.GetName().Version?.ToString(4) ?? "";
        VersionText.Text = $"outbound-watch {version} · MIT license · github.com/strongthium/outbound-watch";
    }

    public void Refresh()
    {
        var app = App.Current;
        showing = true;
        var watcher = app.Watcher;
        ModeText.Text = Program.Sample ? "Showing sample data: a made-up PC. Nothing on this one is watched or saved."
            : watcher.Measuring
                ? "Running as administrator: every TCP and UDP connection is watched, with the bytes it sends and receives, from Windows' own network events. Names come from the DNS lookups as they happen."
                : "Without administrator rights, outbound-watch reads Windows' table of TCP connections once a second. It can't count bytes, see UDP (QUIC, DNS, games, calls), or see connections shorter than a second. "
                  + watcher.CaptureError;
        AdminButton.Show(!Program.Sample && !Program.IsAdministrator);
        LocalNetwork.IsChecked = app.WatchLocalNetwork;
        Reverse.IsChecked = app.ReverseLookups;
        Notify.IsChecked = app.Notify;
        UpdatePlaces.IsChecked = app.UpdatePlaces;
        Keep.SelectedIndex = Math.Max(0, Array.FindIndex(KeepChoices, c => c.Days == app.KeepDays));
        ShowPlaces();
        HistoryText.Text = $"{Sizes.Count(app.Database.HistorySize)} connections kept" + (Program.Sample ? "" : $", in {app.Database.Directory}");

        if (Program.Sample)
        {
            StartAtSignIn.IsEnabled = false;
            AutostartText.Text = "";
        }
        else
        {
            var task = Autostart.TaskEnabled;
            StartAtSignIn.IsChecked = task || Autostart.RunKeyEnabled;
            StartAtSignIn.IsEnabled = Program.IsAdministrator || !task;
            AutostartText.Text = task && !Program.IsAdministrator ? "It starts with administrator rights, from the Task Scheduler: turning it off needs them too."
                : Program.IsAdministrator ? "With administrator rights, from the Task Scheduler: Windows doesn't ask at each sign-in."
                : "Without administrator rights. Turned on while running as administrator, it starts with them, so that bytes and UDP are watched.";
        }
        showing = false;
    }

    private void ShowPlaces()
    {
        var app = App.Current;
        var places = app.Places;
        PlacesText.Text = Program.Sample ? "The sample's places are made up."
            : app.LoadingPlaces ? "Reading the address databases…"
            : places is null ? "Not downloaded. DB-IP's free databases give each address's country and network (its AS number and organization): about 12 MB, kept on this PC, so that nothing is looked up online."
            : $"DB-IP's databases of {places.Month}: {Sizes.Count(places.Ranges)} address ranges, in {app.Database.Directory}.";
        DownloadButton.Content = places is null ? "Download" : "Update now";
        DownloadButton.IsEnabled = !Program.Sample && !app.LoadingPlaces;
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        DownloadButton.IsEnabled = false;
        try
        {
            await App.Current.DownloadPlacesAsync(new Progress<string>(text => DownloadStatus.Text = text), CancellationToken.None);
            DownloadStatus.Text = "Done.";
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or InvalidDataException or TaskCanceledException)
        {
            DownloadStatus.Text = "Couldn't download them: " + exception.Message;
        }
        ShowPlaces();
    }

    private void Admin_Click(object sender, RoutedEventArgs e) => App.Current.RestartAsAdministrator();

    private void LocalNetwork_Click(object sender, RoutedEventArgs e) => App.Current.WatchLocalNetwork = LocalNetwork.IsChecked == true;

    private void Reverse_Click(object sender, RoutedEventArgs e) => App.Current.ReverseLookups = Reverse.IsChecked == true;

    private void Notify_Click(object sender, RoutedEventArgs e) => App.Current.Notify = Notify.IsChecked == true;

    private void UpdatePlaces_Click(object sender, RoutedEventArgs e) => App.Current.UpdatePlaces = UpdatePlaces.IsChecked == true;

    private void Keep_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!showing && Keep.SelectedIndex >= 0) App.Current.KeepDays = KeepChoices[Keep.SelectedIndex].Days;
    }

    private void StartAtSignIn_Click(object sender, RoutedEventArgs e)
    {
        try { Autostart.Set(StartAtSignIn.IsChecked == true); }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException or IOException or System.Security.SecurityException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(Window.GetWindow(this), exception.Message, "outbound-watch", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        Refresh();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!Program.Sample) Process.Start(new ProcessStartInfo(App.Current.Database.Directory) { UseShellExecute = true });
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Window.GetWindow(this), "Delete every connection in the history? The expected lists and settings stay.", "outbound-watch",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        App.Current.Watcher.Save();
        App.Current.Database.ClearHistory();
        Refresh();
    }

    private void Link_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
