namespace PTXChromeMonitor.Services;

public sealed class NotificationService
{
    private readonly NotifyIcon _icon;

    public NotificationService(NotifyIcon icon) => _icon = icon;

    public void Show(string title, string text)
    {
        try
        {
            _icon.ShowBalloonTip(10000, title, text, ToolTipIcon.Warning);
        }
        catch
        {
            // notification failure must never crash the monitor
        }
    }
}
