using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Strongthium.OutboundWatch.App;

/// <summary>A page of the main window, shown again when what it shows may have changed.</summary>
public interface IPage
{
    void Refresh();
}

/// <summary>
/// The connections: Live, History, Totals, Expected, Settings and About, chosen down the side. Closing it hides it; the
/// watching goes on in the tray.
/// </summary>
public sealed partial class MainWindow : Window
{
    public const int LivePage = 0, HistoryPage = 1, TotalsPage = 2, ExpectedPage = 3, SettingsPage = 4, AboutPage = 5;

    private readonly RadioButton[] nav;
    private readonly Func<FrameworkElement>[] makers =
    [
        () => new LivePage(), () => new HistoryPage(), () => new TotalsPage(), () => new ExpectedPage(), () => new SettingsPage(), () => new AboutPage(),
    ];
    private readonly FrameworkElement?[] pages = new FrameworkElement?[6];
    private bool allowClose;

    public MainWindow()
    {
        InitializeComponent();
        Icon = App.Icon;
        Logo.Source = App.Icon;
        nav = [LiveNav, HistoryNav, TotalsNav, ExpectedNav, SettingsNav, AboutNav];
        ShowMode();
        Navigate(Program.StartView switch
        {
            "history" => HistoryPage,
            "totals" => TotalsPage,
            "expected" => ExpectedPage,
            "settings" => SettingsPage,
            _ => LivePage,
        });
    }

    /// <summary>What's being watched, under the pages: everything, or without bytes and UDP.</summary>
    private void ShowMode()
    {
        var watcher = App.Current.Watcher;
        if (Program.Sample)
        {
            ModeIcon.Text = "";
            ModeText.Text = "Sample data: a made-up PC. Nothing on this one is watched.";
        }
        else if (watcher.Measuring)
        {
            ModeIcon.Text = "";
            ModeText.Text = "Watching TCP and UDP, with the bytes each connection sends and receives.";
        }
        else
        {
            ModeIcon.Text = "";
            ModeText.Text = "Watching TCP only, without bytes: " + watcher.CaptureError;
        }
        AdminButton.Show(!Program.Sample && !Program.IsAdministrator);
    }

    public void AllowClose() => allowClose = true;

    /// <summary>Shows a page, made the first time.</summary>
    public FrameworkElement Navigate(int index)
    {
        var page = pages[index] ??= makers[index]();
        if (nav[index].IsChecked != true) nav[index].IsChecked = true;
        if (Page.Content != page)
        {
            Page.Content = page;
            (page as IPage)?.Refresh();
        }
        return page;
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (nav is not null) Navigate(Array.IndexOf(nav, sender));
    }

    /// <summary>Shows what's open now that isn't on an expected list.</summary>
    public void ShowUnexpected() => ((LivePage)Navigate(LivePage)).ShowUnexpected();

    /// <summary>Shows the history of the connections a line of the totals is made of.</summary>
    public void ShowHistory(string label, Func<Connection, bool> filter, int range) =>
        ((HistoryPage)Navigate(HistoryPage)).ShowOnly(label, filter, range);

    /// <summary>Adds a rule for the connection to an expected list, as the user chooses.</summary>
    public void Expect(Connection connection)
    {
        var dialog = new ExpectDialog(connection) { Owner = this };
        if (dialog.ShowDialog() == true) App.Current.ListsChanged();
    }

    /// <summary>Shows the lists or the databases as they are now, on the page that's showing.</summary>
    public void Refresh() => (Page.Content as IPage)?.Refresh();

    private void Admin_Click(object sender, RoutedEventArgs e) => App.Current.RestartAsAdministrator();

    protected override void OnClosing(CancelEventArgs e)
    {
        // The X hides the window, so that the watching goes on in the tray.
        if (!allowClose)
        {
            e.Cancel = true;
            Hide();
            App.Current.WindowClosed();
        }
        base.OnClosing(e);
    }
}
