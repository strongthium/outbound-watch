using System.Diagnostics;
using System.Security;
using Microsoft.Win32;

namespace Strongthium.OutboundWatch.App;

/// <summary>
/// Starts outbound-watch, in the tray, when the user signs in to Windows. Without administrator rights, that's a value
/// in the Run key of HKEY_CURRENT_USER, and it starts without them too. As administrator, it's a task in the Task
/// Scheduler that starts it with them (Windows asks nothing at sign-in), so that bytes and UDP are watched from the
/// start; making the task needs administrator rights itself.
/// </summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "Strongthium outbound-watch";

    private static string Executable => Environment.ProcessPath ?? throw new InvalidOperationException("The program's path is unknown.");

    private static string Arguments => "--minimized" + (Program.OwnDataDirectory ? $" --data \"{Program.DataDirectory}\"" : "");

    public static bool RunKeyEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(Name) is string;
        }
    }

    public static bool TaskEnabled => Schtasks($"/Query /TN \"{Name}\"") == 0;

    public static bool Enabled => RunKeyEnabled || TaskEnabled;

    /// <summary>Turns it on (as administrator, with administrator rights) or off. Throws if it couldn't be.</summary>
    public static void Set(bool on)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(RunKey)) key.DeleteValue(Name, throwOnMissingValue: false);
        if (Program.IsAdministrator && TaskEnabled && Schtasks($"/Delete /TN \"{Name}\" /F") != 0)
            throw new InvalidOperationException("The sign-in task couldn't be deleted.");
        if (!on) return;
        if (Program.IsAdministrator) CreateTask();
        else
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            key.SetValue(Name, $"\"{Executable}\" {Arguments}");
        }
    }

    /// <summary>The task: at this user's sign-in, with the highest rights they have, never stopped after a time, and
    /// on battery too (the Task Scheduler's defaults stop a task after 3 days, and don't start one on battery).</summary>
    private static void CreateTask()
    {
        var user = SecurityElement.Escape(System.Security.Principal.WindowsIdentity.GetCurrent().Name);
        var xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>Starts outbound-watch in the tray, with administrator rights, at sign-in.</Description></RegistrationInfo>
              <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>{user}</UserId></LogonTrigger></Triggers>
              <Principals><Principal id="Author"><UserId>{user}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author"><Exec><Command>{SecurityElement.Escape(Executable)}</Command><Arguments>{SecurityElement.Escape(Arguments)}</Arguments></Exec></Actions>
            </Task>
            """;
        var file = Path.Combine(Path.GetTempPath(), "outbound-watch-task.xml");
        File.WriteAllText(file, xml, System.Text.Encoding.Unicode);
        try
        {
            if (Schtasks($"/Create /TN \"{Name}\" /XML \"{file}\" /F") != 0) throw new InvalidOperationException("The sign-in task couldn't be made.");
        }
        finally { File.Delete(file); }
    }

    private static int Schtasks(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }

    /// <summary>Points the entry at this copy of the program, in case it was moved or updated in another folder. Not
    /// done by a copy started with its own data folder, which would take the entry over from the usual one.</summary>
    public static void Refresh()
    {
        if (Program.OwnDataDirectory) return;
        try
        {
            if (RunKeyEnabled && !Program.IsAdministrator) Set(true);
            else if (Program.IsAdministrator && TaskEnabled) Set(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or SecurityException or System.ComponentModel.Win32Exception)
        {
            Trace.WriteLine("outbound-watch: autostart: " + exception.Message);
        }
    }
}
