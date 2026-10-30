using System.Runtime.InteropServices;

namespace Strongthium.OutboundWatch;

/// <summary>A process: its file name, its full path, and the services in it.</summary>
public sealed record ProcessInfo(string Name, string Path, string Services);

/// <summary>
/// Names a process from its id: the program's path (QueryFullProcessImageName, which any user can ask of most
/// processes; an administrator, of nearly all), and for a <c>svchost.exe</c>, the services it runs. Remembered by id
/// and creation time, so that an id Windows reuses for another process is named again.
/// </summary>
public sealed unsafe class Processes
{
    private readonly Dictionary<int, (long Created, ProcessInfo Info)> known = [];
    private Dictionary<int, string> services = [];
    private DateTime servicesRead;
    private readonly Lock gate = new();

    public ProcessInfo Get(int pid)
    {
        if (pid == 0) return new("System Idle Process", "", "");
        if (pid == 4) return new("System", "", "");
        lock (gate)
        {
            var created = CreationTime(pid);
            if (known.TryGetValue(pid, out var entry) && entry.Created == created && created != 0) return entry.Info;
            var path = ImagePath(pid);
            var name = path.Length > 0 ? System.IO.Path.GetFileName(path) : "";
            var hosted = ServicesOf(pid, name);
            // A protected service's process can't be opened without administrator rights, but its service's program
            // can be read by anyone.
            if (path.Length == 0 && hosted.Length > 0)
            {
                path = ServiceProgram(hosted.Split(", ")[0]);
                name = System.IO.Path.GetFileName(path);
            }
            if (name.Length == 0) name = $"PID {pid}";
            var info = new ProcessInfo(name, path, hosted);
            if (created != 0) known[pid] = (created, info);
            return info;
        }
    }

    private string ServicesOf(int pid, string name)
    {
        if (name.Length > 0 && !name.Equals("svchost.exe", StringComparison.OrdinalIgnoreCase) && !name.Equals("lsass.exe", StringComparison.OrdinalIgnoreCase)
            && !name.Equals("services.exe", StringComparison.OrdinalIgnoreCase)) return "";
        if (!services.ContainsKey(pid) && DateTime.UtcNow - servicesRead > TimeSpan.FromSeconds(5))
        {
            services = ReadServices();
            servicesRead = DateTime.UtcNow;
        }
        return services.GetValueOrDefault(pid, "");
    }

    private static long CreationTime(int pid)
    {
        var handle = Native.OpenProcess(Native.ProcessQueryLimitedInformation, false, (uint)pid);
        if (handle == 0) return 0;
        try { return Native.GetProcessTimes(handle, out var created, out _, out _, out _) ? created : 0; }
        finally { Native.CloseHandle(handle); }
    }

    private static string ImagePath(int pid)
    {
        var handle = Native.OpenProcess(Native.ProcessQueryLimitedInformation, false, (uint)pid);
        if (handle == 0) return "";
        try
        {
            var buffer = stackalloc char[1024];
            uint size = 1024;
            return Native.QueryFullProcessImageName(handle, 0, buffer, ref size) ? new string(buffer, 0, (int)size) : "";
        }
        finally { Native.CloseHandle(handle); }
    }

    /// <summary>The program a service runs, from its command line: <c>C:\Windows\system32\svchost.exe</c> from
    /// <c>C:\Windows\system32\svchost.exe -k NetworkService -p</c>.</summary>
    private static string ServiceProgram(string service)
    {
        var manager = Native.OpenSCManager(null, null, Native.ScManagerEnumerateService);
        if (manager == 0) return "";
        try
        {
            nint handle;
            fixed (char* name = service) handle = Native.OpenService(manager, name, Native.ServiceQueryConfig);
            if (handle == 0) return "";
            try
            {
                var buffer = new byte[8192];
                fixed (byte* p = buffer)
                {
                    if (!Native.QueryServiceConfig(handle, p, (uint)buffer.Length, out _)) return "";
                    return ProgramOf(Marshal.PtrToStringUni(*(nint*)(p + 16)) ?? "");
                }
            }
            finally { Native.CloseServiceHandle(handle); }
        }
        finally { Native.CloseServiceHandle(manager); }
    }

    internal static string ProgramOf(string commandLine)
    {
        commandLine = Environment.ExpandEnvironmentVariables(commandLine.Trim());
        if (commandLine.StartsWith('"'))
        {
            var end = commandLine.IndexOf('"', 1);
            return end > 0 ? commandLine[1..end] : commandLine.Trim('"');
        }
        var exe = commandLine.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe > 0 ? commandLine[..(exe + 4)] : commandLine.Split(' ')[0];
    }

    /// <summary>The running services by process: "Dnscache, NlaSvc" for one svchost.</summary>
    private static Dictionary<int, string> ReadServices()
    {
        var byPid = new Dictionary<int, List<string>>();
        var manager = Native.OpenSCManager(null, null, Native.ScManagerEnumerateService);
        if (manager == 0) return [];
        try
        {
            uint resume = 0;
            Native.EnumServicesStatusEx(manager, 0, Native.ServiceWin32, Native.ServiceActive, null, 0, out var needed, out _, ref resume, null);
            if (needed == 0) return [];
            var buffer = new byte[needed + 4096];
            resume = 0;
            fixed (byte* p = buffer)
            {
                if (!Native.EnumServicesStatusEx(manager, 0, Native.ServiceWin32, Native.ServiceActive, p, (uint)buffer.Length, out _, out var count, ref resume, null))
                    return [];
                // ENUM_SERVICE_STATUS_PROCESSW: the name and display name (pointers), then SERVICE_STATUS_PROCESS, whose
                // process id is its eighth 4-byte field.
                var stride = 2 * IntPtr.Size + 36 + (IntPtr.Size == 8 ? 4 : 0);
                for (var i = 0; i < count; i++)
                {
                    var entry = p + i * stride;
                    var name = Marshal.PtrToStringUni(*(nint*)entry) ?? "";
                    var pid = *(int*)(entry + 2 * IntPtr.Size + 28);
                    if (pid == 0 || name.Length == 0) continue;
                    if (!byPid.TryGetValue(pid, out var list)) byPid[pid] = list = [];
                    list.Add(name);
                }
            }
        }
        finally { Native.CloseServiceHandle(manager); }
        return byPid.ToDictionary(pair => pair.Key, pair => string.Join(", ", pair.Value.Order(StringComparer.OrdinalIgnoreCase)));
    }
}
