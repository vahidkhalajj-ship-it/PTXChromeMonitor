namespace PTXChromeMonitor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "PTXChromeMonitor_SingleInstance", out bool created);
        if (!created) return;

        ApplicationConfiguration.Initialize();
        bool startHidden = args.Contains("--tray");
        Application.Run(new MainForm(startHidden));
    }
}
