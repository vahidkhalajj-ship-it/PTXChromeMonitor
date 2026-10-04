using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using PTXChromeMonitor.Models;

namespace PTXChromeMonitor.Services;

public sealed class SettingsService
{
    private const string TaskName = "PTXChromeMonitor";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PTXChromeMonitor");

    private string SettingsPath => Path.Combine(_dir, "settings.json");
    private string UsagePath => Path.Combine(_dir, "usage.json");

    public AppSettings LoadSettings() => Load<AppSettings>(SettingsPath) ?? new AppSettings();
    public void SaveSettings(AppSettings settings) => Save(SettingsPath, settings);

    public UsageData LoadUsage() => Load<UsageData>(UsagePath) ?? new UsageData();
    public void SaveUsage(UsageData usage) => Save(UsagePath, usage);

    private static T? Load<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private void Save<T>(string path, T value)
    {
        try
        {
            Directory.CreateDirectory(_dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, JsonOptions));
            File.Move(tmp, path, true);
        }
        catch
        {
            // saving is best-effort
        }
    }

    /// <summary>
    /// The app needs Administrator (ETW). A normal Run-key entry would trigger a UAC prompt
    /// at every logon, so we register a Task Scheduler task that runs with highest privileges.
    /// </summary>
    public void SetAutoStart(bool enable)
    {
        if (!enable)
        {
            RunSchtasks($"/Delete /TN \"{TaskName}\" /F", ignoreErrors: true);
            return;
        }

        string exe = Environment.ProcessPath
            ?? throw new InvalidOperationException("مسیر برنامه پیدا نشد.");
        string user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name) ?? "";
        string exeEscaped = SecurityElement.Escape(exe) ?? exe;

        string xml = $"""
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{user}</UserId>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <UserId>{user}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Enabled>true</Enabled>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>{exeEscaped}</Command>
      <Arguments>--tray</Arguments>
    </Exec>
  </Actions>
</Task>
""";

        string tmp = Path.Combine(Path.GetTempPath(), "ptxcm_task.xml");
        File.WriteAllText(tmp, xml, Encoding.Unicode);
        try
        {
            RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{tmp}\" /F", ignoreErrors: false);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    private static void RunSchtasks(string args, bool ignoreErrors)
    {
        var psi = new ProcessStartInfo("schtasks.exe", args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("schtasks اجرا نشد.");
        string err = p.StandardError.ReadToEnd();
        p.StandardOutput.ReadToEnd();
        p.WaitForExit();

        if (p.ExitCode != 0 && !ignoreErrors)
            throw new InvalidOperationException(err.Trim());
    }
}
