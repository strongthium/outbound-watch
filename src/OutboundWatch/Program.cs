using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using System.Windows;

namespace Strongthium.OutboundWatch.App;

/// <summary>
/// The command line: <c>--minimized</c> starts in the tray (as it does at sign-in); <c>--data folder</c> keeps the
/// history somewhere else; <c>--sample</c> shows a made-up PC instead of this one, nothing watched or saved;
/// <c>--theme light|dark</c> sets the look rather than following Windows; <c>--view live|history|totals|expected|settings</c>
/// opens on that page (for the README's pictures).
/// </summary>
internal static class Program
{
    public static bool StartMinimized { get; private set; }
    public static bool Sample { get; private set; }
    public static string? StartView { get; private set; }

    /// <summary>Where the history and the address databases are kept: <see cref="Database.DefaultDirectory"/>, or
    /// the folder given with <c>--data</c>.</summary>
    public static string DataDirectory { get; private set; } = Database.DefaultDirectory;
    public static bool OwnDataDirectory { get; private set; }

    /// <summary>Running as administrator: the kernel's network events can be read.</summary>
    public static bool IsAdministrator { get; } = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    // One a user and data folder (and one for the sample, which saves nothing).
    private static string PipeName => "strongthium-outbound-watch-" + Environment.UserName + (Sample ? "-sample" : "")
        + (OwnDataDirectory ? "-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(DataDirectory.ToUpperInvariant())))[..16] : "");

    [STAThread]
    private static int Main(string[] args)
    {
        string? theme = null;
        int? waitFor = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--minimized": StartMinimized = true; break;
                case "--sample": Sample = true; break;
                case "--theme" when i + 1 < args.Length: theme = args[++i]; break;
                case "--view" when i + 1 < args.Length: StartView = args[++i]; break;
                case "--data" when i + 1 < args.Length:
                    DataDirectory = Path.GetFullPath(args[++i]);
                    OwnDataDirectory = true;
                    break;
                case "--wait-for" when i + 1 < args.Length && int.TryParse(args[i + 1], out var pid):
                    waitFor = pid;
                    i++;
                    break;
            }
        }

        // Restarted as administrator: the copy that started this one is quitting, and lets go of the lock as it does.
        if (waitFor is { } other)
        {
            try { Process.GetProcessById(other).WaitForExit(15_000); }
            catch (ArgumentException) { }
        }

        Directory.CreateDirectory(DataDirectory);
        // One copy a user: two would watch and save everything twice. Starting it again shows the copy that's running.
        FileStream? instanceLock = null;
        if (!Sample)
        {
            try
            {
                instanceLock = new FileStream(Path.Combine(DataDirectory, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                if (!StartMinimized) SignalRunningCopy();
                return 0;
            }
        }

        using (instanceLock)
        {
            var app = new App();
            app.InitializeComponent();
            app.ThemeMode = theme switch
            {
                "light" => ThemeMode.Light,
                "dark" => ThemeMode.Dark,
                _ => ThemeMode.System,
            };
            return app.Run();
        }
    }

    /// <summary>Starts this program again as administrator (Windows asks first), to quit this copy once it has
    /// started. False if it wasn't allowed.</summary>
    public static bool RestartAsAdministrator()
    {
        var path = Environment.ProcessPath;
        if (path is null) return false;
        var arguments = $"--wait-for {Environment.ProcessId}" + (OwnDataDirectory ? $" --data \"{DataDirectory}\"" : "");
        try
        {
            Process.Start(new ProcessStartInfo(path, arguments) { UseShellExecute = true, Verb = "runas" });
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Not allowed, or the question was answered no.
            return false;
        }
    }

    private static void SignalRunningCopy()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.None);
            client.Connect(2000);
            client.WriteByte(1);
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or UnauthorizedAccessException)
        {
            // The other copy is starting or closing; nothing to show.
        }
    }

    /// <summary>Calls <paramref name="show"/> each time outbound-watch is started again while this copy runs. The user
    /// may write to the pipe whether or not this copy runs as administrator, which a pipe made by default doesn't
    /// allow.</summary>
    public static void ListenForOtherCopies(Action show)
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User!, PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow));
        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = NamedPipeServerStreamAcl.Create(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None, 0, 0, security);
                    server.WaitForConnection();
                    if (server.ReadByte() == 1) show();
                }
                catch (IOException)
                {
                    Thread.Sleep(1000);
                }
            }
        }) { IsBackground = true, Name = "outbound-watch pipe" };
        thread.Start();
    }
}
