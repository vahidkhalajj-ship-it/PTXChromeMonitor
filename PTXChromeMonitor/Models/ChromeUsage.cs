namespace PTXChromeMonitor.Models;

public class ChromeUsage
{
    public long DownloadBytes { get; set; }
    public long UploadBytes { get; set; }
    public long TotalBytes => DownloadBytes + UploadBytes;
    public double DownSpeed { get; set; }
    public double UpSpeed { get; set; }
    public bool ChromeRunning { get; set; }
}

public static class ByteFormat
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string Bytes(double b)
    {
        int i = 0;
        while (b >= 1024 && i < Units.Length - 1)
        {
            b /= 1024;
            i++;
        }
        return i == 0 ? $"{b:0} {Units[i]}" : $"{b:0.##} {Units[i]}";
    }

    public static string Speed(double bytesPerSecond) => Bytes(bytesPerSecond) + "/s";
}
