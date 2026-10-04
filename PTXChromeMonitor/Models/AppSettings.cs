using System.Text.Json.Serialization;

namespace PTXChromeMonitor.Models;

public class AppSettings
{
    public double Threshold { get; set; } = 1;
    public string Unit { get; set; } = "GB";
    public bool SoundEnabled { get; set; } = true;
    public bool NotificationEnabled { get; set; } = true;
    public bool AutoStart { get; set; } = false;
    public bool AlertsEnabled { get; set; } = true;

    [JsonIgnore]
    public long ThresholdBytes => (long)(Threshold * UnitFactor(Unit));

    public static double UnitFactor(string unit) => unit switch
    {
        "TB" => 1024.0 * 1024 * 1024 * 1024,
        "GB" => 1024.0 * 1024 * 1024,
        _ => 1024.0 * 1024,
    };

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}

public class UsageData
{
    public long DownloadBytes { get; set; }
    public long UploadBytes { get; set; }
    public bool AlertTriggered { get; set; }
}
