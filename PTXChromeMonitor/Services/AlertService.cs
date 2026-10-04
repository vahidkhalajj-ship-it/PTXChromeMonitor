using System.Media;
using PTXChromeMonitor.Models;

namespace PTXChromeMonitor.Services;

public sealed class AlertService
{
    /// <summary>True after the alert fired; stays true until the user presses Reset.</summary>
    public bool AlertTriggered { get; set; }

    public void Evaluate(long totalBytes, AppSettings settings, NotificationService notifier)
    {
        if (!settings.AlertsEnabled || AlertTriggered) return;
        if (settings.ThresholdBytes <= 0 || totalBytes < settings.ThresholdBytes) return;

        AlertTriggered = true;

        if (settings.SoundEnabled) PlaySound();
        if (settings.NotificationEnabled)
        {
            notifier.Show("⚠️ PTX Chrome Monitor",
                $"مصرف اینترنت Chrome به {ByteFormat.Bytes(totalBytes)} رسید.");
        }
    }

    public static void PlaySound() => SystemSounds.Exclamation.Play();
}
