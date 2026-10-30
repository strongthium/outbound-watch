using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Strongthium.OutboundWatch.App;

/// <summary>What can be done with a connection in a list (Live or History): its right-click menu and double-click.</summary>
internal static class Actions
{
    public static ConnectionRow? Selected(ListView list) => list.SelectedItem as ConnectionRow;

    /// <summary>Sets the menu up for the line it's opening on; no menu off a line.</summary>
    public static void Opening(ListView list, ContextMenuEventArgs e, MenuItem copyName, MenuItem showProgram)
    {
        if (Selected(list) is not { } row)
        {
            e.Handled = true;
            return;
        }
        copyName.Show(row.C.Host.Length > 0);
        showProgram.Show(row.C.ProcessPath.Length > 0);
    }

    public static void Expect(ListView list)
    {
        if (Selected(list) is { } row) list.MainWindowOf().Expect(row.C);
    }

    /// <summary>The history of the destination, a week of it: by name when it has one, or by address.</summary>
    public static void History(ListView list)
    {
        if (Selected(list) is not { } row) return;
        var c = row.C;
        if (c.Host.Length > 0)
        {
            var host = c.Host;
            list.MainWindowOf().ShowHistory(host, x => x.Host.Equals(host, StringComparison.OrdinalIgnoreCase), Ranges.Week);
        }
        else
        {
            var address = c.Remote;
            list.MainWindowOf().ShowHistory(c.Address, x => x.Remote == address, Ranges.Week);
        }
    }

    public static void CopyAddress(ListView list)
    {
        if (Selected(list) is { } row) Copy(row.C.Address);
    }

    public static void CopyName(ListView list)
    {
        if (Selected(list) is { } row) Copy(row.C.Host);
    }

    public static void ShowProgram(ListView list)
    {
        if (Selected(list) is not { } row || !File.Exists(row.C.ProcessPath)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{row.C.ProcessPath}\"") { UseShellExecute = true });
    }

    private static void Copy(string text)
    {
        // The clipboard can be held by another program for a moment.
        try { Clipboard.SetText(text); }
        catch (System.Runtime.InteropServices.COMException) { }
    }

    public static MainWindow MainWindowOf(this DependencyObject element) => (MainWindow)Window.GetWindow(element);

    /// <summary>The column a heading sorts, and which way: the same one again turns the order round.</summary>
    public static (string Column, bool Descending) Sort(object sender, string column, bool descending, Func<string, bool> largestFirst)
    {
        if (sender is not GridViewColumnHeader { Tag: string tag }) return (column, descending);
        return tag == column ? (column, !descending) : (tag, largestFirst(tag));
    }
}
