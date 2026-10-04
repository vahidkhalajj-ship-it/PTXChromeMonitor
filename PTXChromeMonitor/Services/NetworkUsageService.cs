using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using PTXChromeMonitor.Models;

namespace PTXChromeMonitor.Services;

/// <summary>
/// Measures real network bytes (TCP + UDP, IPv4 + IPv6) of all chrome.exe processes
/// using the Windows kernel ETW provider. Requires Administrator.
/// </summary>
public sealed class NetworkUsageService : IDisposable
{
    private readonly ChromeProcessService _chrome = new();

    // Traffic seen for PIDs that were not (yet) known as Chrome. Claimed on the next refresh,
    // so a freshly started Chrome process does not lose its first bytes.
    private readonly ConcurrentDictionary<int, long[]> _pending = new();

    private volatile HashSet<int> _pids = new();
    private long _down;
    private long _up;

    private long _lastDown;
    private long _lastUp;
    private long _lastTick = Stopwatch.GetTimestamp();

    private TraceEventSession? _session;
    private System.Threading.Timer? _timer;

    public string? StartError { get; private set; }
    public long DownloadBytes => Interlocked.Read(ref _down);
    public long UploadBytes => Interlocked.Read(ref _up);

    public void Start()
    {
        RefreshPids();
        _timer = new System.Threading.Timer(_ => RefreshPids(), null, 500, 500);

        try
        {
            _session = new TraceEventSession("PTXChromeMonitorSession");
            _session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);
            _session.Source.Kernel.All += OnKernelEvent;

            var session = _session;
            new Thread(() =>
            {
                try { session.Source.Process(); }
                catch (Exception ex) { StartError = ex.Message; }
            })
            { IsBackground = true, Name = "PTX-ETW" }.Start();
        }
        catch (Exception ex)
        {
            StartError = ex.Message;
        }
    }

    public void SetTotals(long download, long upload)
    {
        Interlocked.Exchange(ref _down, download);
        Interlocked.Exchange(ref _up, upload);
        _lastDown = download;
        _lastUp = upload;
        _lastTick = Stopwatch.GetTimestamp();
    }

    public void Reset() => SetTotals(0, 0);

    /// <summary>Call once per second from the UI thread: totals + current speeds.</summary>
    public ChromeUsage Snapshot()
    {
        long now = Stopwatch.GetTimestamp();
        double seconds = (now - _lastTick) / (double)Stopwatch.Frequency;
        if (seconds < 0.1) seconds = 0.1;

        long d = DownloadBytes;
        long u = UploadBytes;
        bool running = _pids.Count > 0;

        var usage = new ChromeUsage
        {
            DownloadBytes = d,
            UploadBytes = u,
            ChromeRunning = running,
            DownSpeed = running && d >= _lastDown ? (d - _lastDown) / seconds : 0,
            UpSpeed = running && u >= _lastUp ? (u - _lastUp) / seconds : 0,
        };

        _lastDown = d;
        _lastUp = u;
        _lastTick = now;
        return usage;
    }

    private void RefreshPids()
    {
        var fresh = _chrome.GetChromePids();
        _pids = fresh;

        foreach (var pid in _pending.Keys.ToArray())
        {
            if (_pending.TryRemove(pid, out var bytes) && fresh.Contains(pid))
            {
                Interlocked.Add(ref _down, Interlocked.Read(ref bytes[0]));
                Interlocked.Add(ref _up, Interlocked.Read(ref bytes[1]));
            }
        }
    }

    private void OnKernelEvent(TraceEvent data)
    {
        string? name = data.EventName;
        if (name is null) return;

        bool tcp = name.StartsWith("TcpIp", StringComparison.Ordinal);
        bool udp = name.StartsWith("UdpIp", StringComparison.Ordinal);
        if (!tcp && !udp) return;

        bool recv = name.Contains("Recv", StringComparison.Ordinal);
        bool send = name.Contains("Send", StringComparison.Ordinal);
        if (recv == send) return;

        int size;
        try { size = Convert.ToInt32(data.PayloadByName("size")); }
        catch { return; }
        if (size <= 0) return;

        // Ignore local loopback traffic (not internet usage).
        if (data.PayloadByName("daddr") is IPAddress dst && IPAddress.IsLoopback(dst)) return;
        if (data.PayloadByName("saddr") is IPAddress src && IPAddress.IsLoopback(src)) return;

        int pid = data.ProcessID;

        if (_pids.Contains(pid))
        {
            if (recv) Interlocked.Add(ref _down, size);
            else Interlocked.Add(ref _up, size);
        }
        else
        {
            var bytes = _pending.GetOrAdd(pid, _ => new long[2]);
            Interlocked.Add(ref bytes[recv ? 0 : 1], size);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        try { _session?.Dispose(); } catch { }
    }
}
