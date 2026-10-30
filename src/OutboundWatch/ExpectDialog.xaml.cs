using System.Windows;
using System.Windows.Controls;

namespace Strongthium.OutboundWatch.App;

/// <summary>Adds a rule for a connection to an expected list (or a new one); true once it's saved.</summary>
public sealed partial class ExpectDialog : Window
{
    private const string NewList = "A new list…";
    private readonly Connection connection;
    private readonly List<(RadioButton Button, string Words)> choices = [];
    private bool edited, writing;

    /// <param name="by">Which choice to start on, when it's for a line of the totals.</param>
    public ExpectDialog(Connection connection, GroupBy? by = null)
    {
        InitializeComponent();
        this.connection = connection;
        var c = connection;
        Heading.Text = $"{c.Process} → {c.Destination}";
        Subheading.Text = string.Join(" · ", new[] { Addresses.Format(c.Remote, c.RemotePort), Rows.PlaceText(c.Place) }.Where(s => s.Length > 0));

        // A name only counts when the PC looked it up itself.
        var named = c.Host.Length > 0 && c.HostSource == HostSource.Dns;
        if (named) Choice($"The name {c.Host}", $"host {c.Host}");
        if (named && Totals.Domain(c.Host) is var domain && domain != c.Host) Choice($"Any name in {domain}", $"host *.{domain}");
        Choice($"The address {c.Address}", $"ip {c.Address}");
        if (c.Place.Asn > 0) Choice($"The network {c.Place.Org} (AS{c.Place.Asn})", $"asn {c.Place.Asn}");
        if (c.Place.Country.Length > 0) Choice($"Anywhere in {Rows.CountryName(c.Place.Country)}", $"country {c.Place.Country}", Rows.Flag(c.Place.Country));
        Choice("Anywhere", "");
        var start = by switch
        {
            GroupBy.Program or GroupBy.Port => choices[^1].Button,
            GroupBy.Organization => choices.FirstOrDefault(x => x.Words.StartsWith("asn", StringComparison.Ordinal)).Button,
            GroupBy.Country => choices.FirstOrDefault(x => x.Words.StartsWith("country", StringComparison.Ordinal)).Button,
            GroupBy.Address => choices.First(x => x.Words.StartsWith("ip", StringComparison.Ordinal)).Button,
            _ => null,
        } ?? choices[named && Totals.Domain(c.Host) != c.Host ? 1 : 0].Button;
        start.IsChecked = true;

        OnlyProgram.Content = c.Service.Length > 0 ? $"Only from the service {c.Service.Split(", ")[0]}" : $"Only from {c.Process}";
        OnlyProgram.IsChecked = by != GroupBy.Port;
        OnlyPort.Content = $"Only to {Totals.PortName(c.Protocol, c.RemotePort)}";
        OnlyPort.IsChecked = by == GroupBy.Port;

        foreach (var list in App.Current.Lists) ListBox.Items.Add(list.Name);
        ListBox.Items.Add(NewList);
        ListBox.SelectedIndex = 0;
        NewName.Text = "My expected";
        Write();
    }

    private void Choice(string label, string words, System.Windows.Media.ImageSource? flag = null)
    {
        object content = label;
        if (flag is not null)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = (System.Windows.Media.Brush)FindResource("CardStrokeColorDefaultBrush"),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new System.Windows.Controls.Image { Source = flag, Height = 14, MaxWidth = 28, Stretch = System.Windows.Media.Stretch.Uniform },
            });
            panel.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            content = panel;
        }
        var button = new RadioButton { Content = content, GroupName = "where", Margin = new Thickness(0, 0, 0, 4) };
        button.Checked += Changed;
        Choices.Children.Add(button);
        choices.Add((button, words));
    }

    /// <summary>Writes the rule from the choices, unless it has been changed by hand.</summary>
    private void Write()
    {
        if (edited) return;
        var c = connection;
        var words = new List<string>();
        var where = choices.FirstOrDefault(x => x.Button.IsChecked == true).Words;
        if (!string.IsNullOrEmpty(where)) words.Add(where);
        if (OnlyProgram.IsChecked == true)
        {
            words.Add(c.Service.Length > 0 ? $"service {c.Service.Split(", ")[0]}" : $"process {Quote(c.Process)}");
        }
        if (OnlyPort.IsChecked == true)
        {
            words.Add($"port {c.RemotePort}");
            words.Add($"proto {c.Protocol.ToString().ToLowerInvariant()}");
        }
        writing = true;
        RuleBox.Text = string.Join(" ", words);
        writing = false;
        Check();
    }

    private static string Quote(string value) => value.Contains(' ') ? $"\"{value}\"" : value;

    private void Check()
    {
        var error = Rule.Parse(RuleBox.Text, out var rule);
        if (error.Length == 0 && rule is null) error = "Choose at least one thing for it to match.";
        else if (rule is not null && !rule.Matches(connection)) error = "This rule wouldn't match the connection.";
        RuleError.Text = error;
        RuleError.Show(error.Length > 0);
        AddButton.IsEnabled = rule is not null;
    }

    private void Changed(object sender, RoutedEventArgs e)
    {
        if (IsInitialized && RuleBox is not null) Write();
    }

    private void Rule_Changed(object sender, TextChangedEventArgs e)
    {
        if (writing) return;
        edited = true;
        Check();
    }

    private void List_Changed(object sender, SelectionChangedEventArgs e) =>
        NewName.Visibility = ListBox.SelectedItem as string == NewList ? Visibility.Visible : Visibility.Hidden;

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var app = App.Current;
        ExpectedList list;
        if (ListBox.SelectedItem as string == NewList)
        {
            var name = NewName.Text.Trim();
            if (name.Length == 0) name = "My expected";
            list = new ExpectedList(name, "");
        }
        else list = app.Lists[ListBox.SelectedIndex];
        var text = list.Text.TrimEnd('\n', '\r', ' ');
        list.SetText((text.Length > 0 ? text + "\n" : "") + RuleBox.Text.Trim() + "\n");
        list.Enabled = true;
        app.Database.SaveList(list);
        DialogResult = true;
    }
}
