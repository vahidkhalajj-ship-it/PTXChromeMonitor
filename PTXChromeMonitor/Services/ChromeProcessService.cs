using System.Diagnostics;

namespace PTXChromeMonitor.Services;

public sealed class ChromeProcessService
{
    /// <summary>PID of every running chrome.exe process.</summary>
    public HashSet<int> GetChromePids()
    {
        var set = new HashSet<int>();
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName("chrome");
        }
        catch
        {
            return set;
        }

        foreach (var p in processes)
        {
            try { set.Add(p.Id); }
            catch { /* process exited meanwhile */ }
            finally { p.Dispose(); }
        }
        return set;
    }
}
