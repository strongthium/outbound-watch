using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Strongthium.OutboundWatch.App;

/// <summary>
/// Runs in the background, in the tray: the watcher follows every connection and saves it to the history, and the
/// tray tells of connections that aren't on an expected list. The main window shows them; closing it leaves the
/// watching running.
/// </summary>
public sealed partial class App : Application
{
    public Database Database { get; private set; } = null!;
    public Watcher Watcher { get; private set; } = null!;
    /// <summary>The expected lists, as saved; <see cref="ListsChanged"/> hands them to the watcher.</summary>
    public List<ExpectedList> Lists { get; private set; } = [];
    /// <summary>The address databases, once they're read; null until then, or if they've never been downloaded.</summary>
    public AddressDatabase? Places { get; private set; }
    public bool LoadingPlaces { get; private set; }
    /// <summary>When watching began.</summary>
    public DateTime Started { get; } = DateTime.UtcNow;

    public static new App Current => (App)Application.Current;
    public static ImageSource Icon { get; } = BitmapFrame.Create(new Uri("pack://application:,,,/outbound-watch;component/Assets/outbound-watch.png"));

    private MainWindow? mainWindow;
    private Forms.NotifyIcon? trayIcon;
    private IDisposable? sample;
    private HttpClient? http;
    private readonly Dictionary<string, DateTime> told = [];
    private bool toldAboutTray;

    // ---- Settings ----------------------------------------------------------------------------------------------

    private bool Flag(string key, bool fallback) => Database.Setting(key) is { } value ? value == "on" : fallback;

    private void SetFlag(string key, bool value) => Database.SetSetting(key, value ? "on" : "off");

    /// <summary>Tells, in the tray, of each new program and place that isn't on an expected list.</summary>
    public bool Notify
    {
        get => Flag("notify", true);
        set => SetFlag("notify", value);
    }

    public bool WatchLocalNetwork
    {
        get => Flag("local_network", false);
        set
        {
            SetFlag("local_network", value);
            Watcher.WatchLocalNetwork = value;
        }
    }

    public bool ReverseLookups
    {
        get => Flag("reverse_lookups", true);
        set
        {
            SetFlag("reverse_lookups", value);
            Watcher.ReverseLookups = value && !Program.Sample;
        }
    }

    /// <summary>Downloads DB-IP's new files each month, once they've been downloaded the first time.</summary>
    public bool UpdatePlaces
    {
        get => Flag("update_places", true);
        set => SetFlag("update_places", value);
    }

    /// <summary>Days of history kept: 7, 30, 90 (until set), 365, or 0 for all of it.</summary>
    public int KeepDays
    {
        get => int.TryParse(Database.Setting("keep_days"), CultureInfo.InvariantCulture, out var days) && days >= 0 ? days : 90;
        set
        {
            Database.SetSetting("keep_days", value.ToString(CultureInfo.InvariantCulture));
            Watcher.KeepDays = value;
        }
    }

    // ---- Starting ----------------------------------------------------------------------------------------------

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "outbound-watch", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var now = DateTime.UtcNow;
        Database = Program.Sample ? Database.InMemory() : new Database(Program.DataDirectory);
        if (Program.Sample)
        {
            foreach (var list in SampleData.Lists()) Database.SaveList(list);
        }
        Lists = Database.Lists();
        Watcher = new Watcher(Database, Program.Sample ? SampleData.ProcessOf : null)
        {
            WatchLocalNetwork = WatchLocalNetwork,
            ReverseLookups = ReverseLookups && !Program.Sample,
            KeepDays = KeepDays,
        };
        Watcher.SetLists(Lists);
        Watcher.Unexpected += list => Dispatcher.BeginInvoke(() => Tell(list));
        if (Program.Sample)
        {
            Watcher.SetPlaces(SampleData.Lookup);
            SampleData.Fill(Database, Lists, now);
            Watcher.StartSample();
            sample = SampleData.Run(Watcher, now);
        }
        else
        {
            Watcher.Start(table: true, events: Program.IsAdministrator);
            _ = LoadPlacesAsync();
        }

        // The window first, so that it's up as soon as it can be; the rest once it has been drawn.
        if (!Program.StartMinimized) ShowMainWindow();
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            ShowTrayIcon();
            if (!Program.Sample) Autostart.Refresh();
            Program.ListenForOtherCopies(() => Dispatcher.BeginInvoke(ShowMainWindow));
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (_, _) => UpdateTrayText();
            timer.Start();
        });
    }

    /// <summary>Reads the address databases, then gives them to the watcher; and once a month, when they're due,
    /// downloads the new ones first.</summary>
    private async Task LoadPlacesAsync()
    {
        LoadingPlaces = true;
        try
        {
            var directory = Program.DataDirectory;
            Places = await Task.Run(() => AddressDatabase.Load(directory));
            Watcher.SetPlaces(Places);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.WriteLine("outbound-watch: address databases: " + exception.Message);
        }
        finally
        {
            LoadingPlaces = false;
            mainWindow?.Refresh();
        }
        var month = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        if (Places is not null && UpdatePlaces && string.CompareOrdinal(Places.Month, month) < 0 && DateTime.UtcNow.Day >= 3)
        {
            try { await DownloadPlacesAsync(null, CancellationToken.None); }
            catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or InvalidDataException or TaskCanceledException)
            {
                System.Diagnostics.Trace.WriteLine("outbound-watch: address databases update: " + exception.Message);
            }
        }
    }

    /// <summary>Downloads DB-IP's files for this month and starts using them.</summary>
    public async Task DownloadPlacesAsync(IProgress<string>? progress, CancellationToken cancel)
    {
        http ??= new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        if (http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            var version = typeof(App).Assembly.GetName().Version?.ToString(4) ?? "1.0.0.0";
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"outbound-watch/{version} (+https://github.com/strongthium/outbound-watch)");
        }
        LoadingPlaces = true;
        try
        {
            var directory = Program.DataDirectory;
            await AddressDatabase.DownloadAsync(http, directory, progress, cancel);
            Places = await Task.Run(() => AddressDatabase.Load(directory), cancel);
            Watcher.SetPlaces(Places);
        }
        finally
        {
            LoadingPlaces = false;
            mainWindow?.Refresh();
        }
    }

    /// <summary>The lists were changed: they're saved by then, and the watcher judges what's open again.</summary>
    public void ListsChanged()
    {
        Lists = Database.Lists();
        Watcher.SetLists(Lists);
        mainWindow?.Refresh();
    }

    // ---- The window and the tray -------------------------------------------------------------------------------

    public void ShowMainWindow()
    {
        if (mainWindow is null)
        {
            mainWindow = new MainWindow();
            mainWindow.Closed += (_, _) => mainWindow = null;
        }
        mainWindow.Show();
        if (mainWindow.WindowState == WindowState.Minimized) mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();
    }

    /// <summary>The window was closed: the watching goes on in the tray, which says so the first time.</summary>
    public void WindowClosed()
    {
        if (toldAboutTray || Flag("told_about_tray", false)) return;
        toldAboutTray = true;
        SetFlag("told_about_tray", true);
        trayIcon?.ShowBalloonTip(5000, "Still watching", "outbound-watch keeps watching in the tray. Quit is in its icon's menu.", Forms.ToolTipIcon.Info);
    }

    private void ShowTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open outbound-watch", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Unexpected connections", null, (_, _) =>
        {
            ShowMainWindow();
            mainWindow?.ShowUnexpected();
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit (stop watching)", null, (_, _) => Quit());
        using (var icon = GetResourceStream(new Uri("pack://application:,,,/outbound-watch;component/Assets/outbound-watch.ico")).Stream)
            trayIcon = new Forms.NotifyIcon
            {
                Icon = new System.Drawing.Icon(icon, Forms.SystemInformation.SmallIconSize),
                Text = "outbound-watch",
                ContextMenuStrip = menu,
                Visible = true,
            };
        trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left) ShowMainWindow();
        };
        trayIcon.BalloonTipClicked += (_, _) =>
        {
            if (trayIcon.Tag as string != "unexpected") return;
            ShowMainWindow();
            mainWindow?.ShowUnexpected();
        };
        UpdateTrayText();
    }

    private void UpdateTrayText()
    {
        if (trayIcon is null) return;
        var open = Watcher.Snapshot();
        var unexpected = open.Count(c => c.Verdict == Verdict.Unexpected);
        var text = $"outbound-watch: {open.Count} open" + (unexpected > 0 ? $", {unexpected} unexpected" : "");
        trayIcon.Text = text.Length > 127 ? text[..127] : text;
    }

    /// <summary>Tells of new connections that aren't on an expected list: each program and place once in six hours,
    /// several at once in one message.</summary>
    private void Tell(IReadOnlyList<Connection> connections)
    {
        if (!Notify || trayIcon is null) return;
        var now = DateTime.UtcNow;
        var fresh = new List<Connection>();
        foreach (var c in connections)
        {
            var key = c.Process + "|" + (c.Host.Length > 0 ? Totals.Domain(c.Host) : c.Address);
            if (told.TryGetValue(key, out var at) && now - at < TimeSpan.FromHours(6)) continue;
            told[key] = now;
            fresh.Add(c);
        }
        if (fresh.Count == 0) return;
        var first = fresh[0];
        var where = Rows.PlaceText(first.Place);
        var text = $"{first.Process} → {first.Destination}" + (where.Length > 0 ? $" ({where})" : "");
        if (fresh.Count > 1) text += $"\nand {fresh.Count - 1} more";
        trayIcon.Tag = "unexpected";
        trayIcon.ShowBalloonTip(8000, "Not on an expected list", text, Forms.ToolTipIcon.Warning);
    }

    public void RestartAsAdministrator()
    {
        if (Program.RestartAsAdministrator()) Quit();
    }

    public void Quit()
    {
        mainWindow?.AllowClose();
        mainWindow?.Close();
        if (trayIcon is not null) trayIcon.Visible = false;
        trayIcon?.Dispose();
        sample?.Dispose();
        Watcher.Dispose();
        Database.Dispose();
        Shutdown();
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        // Windows is shutting down: save what's open as ended now.
        Watcher.Dispose();
        base.OnSessionEnding(e);
    }
}
