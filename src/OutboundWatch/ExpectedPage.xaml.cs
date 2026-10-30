using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Strongthium.OutboundWatch.App;

/// <summary>A list on the left of the Expected page.</summary>
public sealed class ListItem(ExpectedList list)
{
    public ExpectedList List { get; } = list;
    public string Name => List.Name;
    public bool Enabled => List.Enabled;
    public string Summary => (List.Rules.Count == 1 ? "1 rule" : $"{List.Rules.Count} rules") + (List.Enabled ? "" : " · off");
}

/// <summary>The expected lists, and the rules of the one chosen, edited as text and saved with Save.</summary>
public sealed partial class ExpectedPage : UserControl, IPage
{
    private static readonly (string Word, string Meaning)[] Words =
    [
        ("host", "the name the program looked up; *.example.com is it and every name under it"),
        ("ip", "an address, or a network: 203.0.113.0/24"),
        ("asn", "the network's AS number: 64500"),
        ("org", "the network's organization, or part of its name"),
        ("country", "where the address is: CA, US, DE"),
        ("process", "the program: firefox.exe, or a path with \\ in it"),
        ("service", "a Windows service inside svchost.exe"),
        ("port", "the port it connects to: 443, or 8000-8100"),
        ("proto", "tcp or udp"),
    ];

    private ExpectedList? current;
    private bool showing, reloading;
    private int counting;

    public ExpectedPage()
    {
        InitializeComponent();
        foreach (var (word, meaning) in Words)
        {
            var row = Help.RowDefinitions.Count;
            Help.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var name = new TextBlock { Text = word, FontFamily = RulesBox.FontFamily, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 12, 6) };
            var text = new TextBlock { Text = meaning, Style = (Style)FindResource("Caption"), FontSize = 12.5, Margin = new Thickness(0, 1, 0, 6) };
            Grid.SetRow(name, row);
            Grid.SetRow(text, row);
            Grid.SetColumn(text, 1);
            Help.Children.Add(name);
            Help.Children.Add(text);
        }
    }

    /// <summary>The lists as saved; the one being changed stays as it is in the editor.</summary>
    public void Refresh() => Reload(current?.Id, keepEditor: Changed);

    private void Reload(long? id, bool keepEditor = false)
    {
        reloading = true;
        Lists.ItemsSource = App.Current.Lists.Select(l => new ListItem(l)).ToList();
        var item = Lists.Items.Cast<ListItem>().FirstOrDefault(i => i.List.Id == id) ?? Lists.Items.Cast<ListItem>().FirstOrDefault();
        Lists.SelectedItem = item;
        reloading = false;
        if (!keepEditor) ShowList(item?.List);
    }

    /// <summary>Shows a list, a copy of it, so that nothing changes until Save.</summary>
    private void ShowList(ExpectedList? list)
    {
        current = list is null ? null : new ExpectedList(list.Name, list.Text, list.Enabled, list.Id);
        showing = true;
        NameBox.Text = list?.Name ?? "";
        RulesBox.Text = list?.Text ?? "";
        showing = false;
        Editor.Show(list is not null);
        NoList.Show(list is null);
        ExportButton.IsEnabled = list is not null;
        ShowEdits();
        _ = CountMatches();
    }

    private bool Changed => current is not null && (NameBox.Text.Trim() != current.Name || RulesBox.Text != current.Text);

    private void Edited(object sender, TextChangedEventArgs e)
    {
        if (!showing) ShowEdits();
    }

    private void ShowEdits()
    {
        SaveButton.IsEnabled = RevertButton.IsEnabled = Changed && NameBox.Text.Trim().Length > 0;
        var errors = new ExpectedList("", RulesBox.Text).Errors;
        ErrorsText.Text = string.Join("\n", errors);
        ErrorsText.Show(errors.Count > 0);
    }

    /// <summary>How many of the last week's connections the list has a rule for.</summary>
    private async Task CountMatches()
    {
        var version = ++counting;
        MatchText.Text = "";
        if (current is null) return;
        var list = new ExpectedList(current.Name, current.Text);
        var app = App.Current;
        var now = DateTime.UtcNow;
        var (matched, total) = await Task.Run(() =>
        {
            var destinations = app.Database.Destinations(now.AddDays(-7), now);
            return (destinations.Where(d => list.Matches(d.Latest)).Sum(d => d.Connections), destinations.Sum(d => d.Connections));
        });
        if (version != counting) return;
        MatchText.Text = total == 0 ? "" : $"Matches {Sizes.Count(matched)} of {Sizes.Count(total)} connections in 7 days";
    }

    private void Save()
    {
        if (current is null) return;
        current.Name = NameBox.Text.Trim();
        current.SetText(RulesBox.Text.Replace("\r\n", "\n"));
        App.Current.Database.SaveList(current);
        App.Current.ListsChanged();
    }

    /// <summary>Whether to go on and leave the list shown: after saving it, or dropping its changes, as asked.</summary>
    private bool LeaveList()
    {
        if (!Changed) return true;
        var answer = MessageBox.Show(Window.GetWindow(this), $"Save the changes to \"{current!.Name}\"?", "outbound-watch", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Cancel) return false;
        if (answer == MessageBoxResult.Yes) Save();
        return true;
    }

    private void Lists_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (reloading || Lists.SelectedItem is not ListItem item || item.List.Id == current?.Id) return;
        if (!LeaveList())
        {
            Lists.SelectedItem = Lists.Items.Cast<ListItem>().FirstOrDefault(i => i.List.Id == current?.Id);
            return;
        }
        ShowList(item.List);
    }

    private void Enabled_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: ListItem item }) return;
        item.List.Enabled = !item.List.Enabled;
        App.Current.Database.SaveList(item.List);
        if (current?.Id == item.List.Id) current.Enabled = item.List.Enabled;
        App.Current.ListsChanged();
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Save();

    private void Revert_Click(object sender, RoutedEventArgs e) => ShowList(App.Current.Lists.FirstOrDefault(l => l.Id == current?.Id));

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (!LeaveList()) return;
        var names = App.Current.Lists.Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = "New list";
        for (var n = 2; names.Contains(name); n++) name = $"New list {n}";
        var list = new ExpectedList(name, "# A rule a line, like:\n# process firefox.exe port 443\n");
        App.Current.Database.SaveList(list);
        current = null;
        App.Current.ListsChanged();
        Reload(list.Id);
        NameBox.Focus();
        NameBox.SelectAll();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (current is null) return;
        if (MessageBox.Show(Window.GetWindow(this), $"Delete the list \"{current.Name}\"?", "outbound-watch", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;
        App.Current.Database.DeleteList(current.Id);
        current = null;
        App.Current.ListsChanged();
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (!LeaveList()) return;
        var dialog = new OpenFileDialog { Filter = "Rules (*.txt)|*.txt|All files|*.*", Title = "Import an expected list" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        var list = new ExpectedList(Path.GetFileNameWithoutExtension(dialog.FileName), File.ReadAllText(dialog.FileName).Replace("\r\n", "\n"));
        App.Current.Database.SaveList(list);
        current = null;
        App.Current.ListsChanged();
        Reload(list.Id);
        if (list.Errors.Count > 0)
            MessageBox.Show(Window.GetWindow(this), $"{list.Errors.Count} of its lines couldn't be read: they're marked under the rules.", "outbound-watch", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (current is null) return;
        var dialog = new SaveFileDialog { Filter = "Rules (*.txt)|*.txt", FileName = current.Name + ".txt", Title = "Export the expected list" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        File.WriteAllText(dialog.FileName, $"# outbound-watch expected list: {current.Name}\n" + RulesBox.Text.Replace("\r\n", "\n"));
    }
}
