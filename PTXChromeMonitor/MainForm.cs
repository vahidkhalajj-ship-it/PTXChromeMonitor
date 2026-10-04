using PTXChromeMonitor.Models;
using PTXChromeMonitor.Services;
using PTXChromeMonitor.UI;

namespace PTXChromeMonitor;

public sealed class MainForm : Form
{
    private readonly SettingsService _store = new();
    private readonly NetworkUsageService _net = new();
    private readonly AlertService _alert = new();
    private readonly NotifyIcon _tray = new();
    private readonly NotificationService _notify;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private readonly bool _startHidden;

    private AppSettings _settings;
    private ChromeUsage _last = new();
    private bool _firstShow = true;
    private bool _exiting;
    private int _saveCounter;
    private ToolStripMenuItem _miAlerts = null!;

    private readonly Label _lblStatus = MakeValue();
    private readonly Label _lblTotal = MakeValue(26f, FontStyle.Bold);
    private readonly Label _lblDown = MakeValue();
    private readonly Label _lblUp = MakeValue();
    private readonly Label _lblSpeed = MakeValue();
    private readonly Label _lblThreshold = MakeValue();
    private readonly Label _lblAlertState = MakeValue();

    public MainForm(bool startHidden)
    {
        _startHidden = startHidden;
        _settings = _store.LoadSettings();

        var usage = _store.LoadUsage();
        _net.SetTotals(usage.DownloadBytes, usage.UploadBytes);
        _alert.AlertTriggered = usage.AlertTriggered;

        _notify = new NotificationService(_tray);

        BuildUi();
        BuildTray();

        _net.Start();
        if (_net.StartError != null)
        {
            _notify.Show("PTX Chrome Monitor", "اندازه‌گیری شبکه شروع نشد. برنامه را با Administrator اجرا کنید.");
        }

        _timer.Tick += OnTick;
        _timer.Start();
        UpdateUi();
    }

    private static Label MakeValue(float size = 11f, FontStyle style = FontStyle.Regular) => new()
    {
        AutoSize = true,
        RightToLeft = RightToLeft.No,
        Font = new Font("Segoe UI", size, style),
        Margin = new Padding(3, 4, 3, 4),
    };

    // ---------- UI ----------

    private void BuildUi()
    {
        Text = "PTX Chrome Monitor";
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(400, 430);
        Icon = SystemIcons.Application;

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(18),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        void AddWide(Control c)
        {
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(c, 0, row);
            table.SetColumnSpan(c, 2);
            row++;
        }

        void AddRow(string caption, Label value)
        {
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(3, 4, 14, 4) }, 0, row);
            table.Controls.Add(value, 1, row);
            row++;
        }

        _lblStatus.MaximumSize = new Size(360, 0);
        AddWide(_lblStatus);
        AddWide(new Label { Text = "مصرف کل Chrome", AutoSize = true, Margin = new Padding(3, 12, 3, 0) });
        AddWide(_lblTotal);
        AddRow("دانلود:", _lblDown);
        AddRow("آپلود:", _lblUp);
        AddRow("سرعت فعلی:", _lblSpeed);
        AddRow("حد هشدار:", _lblThreshold);
        AddRow("وضعیت هشدار:", _lblAlertState);

        AddWide(new Label
        {
            Text = "جزئیات: اطلاعات دقیق در دسترس نیست",
            AutoSize = true,
            ForeColor = Color.Gray,
            Margin = new Padding(3, 12, 3, 3),
        });

        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 16, 0, 0) };
        var btnReset = new Button { Text = "Reset مصرف", AutoSize = true };
        var btnSettings = new Button { Text = "تنظیمات", AutoSize = true };
        btnReset.Click += (_, _) => DoReset();
        btnSettings.Click += (_, _) => OpenSettings();
        buttons.Controls.Add(btnReset);
        buttons.Controls.Add(btnSettings);
        AddWide(buttons);

        Controls.Add(table);

        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized) Hide();
        };
    }

    private void BuildTray()
    {
        var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };

        menu.Items.Add("نمایش برنامه", null, (_, _) => ShowMain());

        _miAlerts = new ToolStripMenuItem("فعال/غیرفعال کردن هشدار") { Checked = _settings.AlertsEnabled };
        _miAlerts.Click += (_, _) =>
        {
            _settings.AlertsEnabled = !_settings.AlertsEnabled;
            _miAlerts.Checked = _settings.AlertsEnabled;
            _store.SaveSettings(_settings);
            UpdateUi();
        };
        menu.Items.Add(_miAlerts);

        menu.Items.Add("تنظیمات", null, (_, _) => OpenSettings());
        menu.Items.Add("Reset مصرف", null, (_, _) => DoReset());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("خروج", null, (_, _) =>
        {
            _exiting = true;
            Application.Exit();
        });

        _tray.Icon = SystemIcons.Application;
        _tray.Text = "PTX Chrome Monitor";
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowMain();
        _tray.Visible = true;
    }

    private void ShowMain()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        UpdateUi();
    }

    protected override void SetVisibleCore(bool value)
    {
        if (_firstShow && _startHidden)
        {
            _firstShow = false;
            value = false;
            if (!IsHandleCreated) CreateHandle();
        }
        base.SetVisibleCore(value);
    }

    // ---------- Monitoring loop ----------

    private void OnTick(object? sender, EventArgs e)
    {
        _last = _net.Snapshot();
        _alert.Evaluate(_last.TotalBytes, _settings, _notify);
        _tray.Text = $"PTX Chrome Monitor - {ByteFormat.Bytes(_last.TotalBytes)}";

        if (Visible) UpdateUi();

        if (++_saveCounter >= 30)
        {
            _saveCounter = 0;
            SaveUsage();
        }
    }

    private void UpdateUi()
    {
        var u = _last;

        _lblTotal.Text = ByteFormat.Bytes(u.TotalBytes);
        _lblDown.Text = ByteFormat.Bytes(u.DownloadBytes);
        _lblUp.Text = ByteFormat.Bytes(u.UploadBytes);
        _lblSpeed.Text = $"↓ {ByteFormat.Speed(u.DownSpeed)}    ↑ {ByteFormat.Speed(u.UpSpeed)}";
        _lblThreshold.Text = $"{_settings.Threshold:0.##} {_settings.Unit}";

        string state = _settings.AlertsEnabled ? "فعال" : "غیرفعال";
        if (_alert.AlertTriggered) state += " (هشدار داده شد)";
        _lblAlertState.Text = state;

        if (_net.StartError != null)
        {
            _lblStatus.Text = "خطا در اندازه‌گیری شبکه: " + _net.StartError;
            _lblStatus.ForeColor = Color.Firebrick;
        }
        else if (u.ChromeRunning)
        {
            _lblStatus.Text = "Chrome در حال اجرا است.";
            _lblStatus.ForeColor = Color.SeaGreen;
        }
        else
        {
            _lblStatus.Text = "Chrome در حال اجرا نیست.";
            _lblStatus.ForeColor = Color.Gray;
        }
    }

    // ---------- Actions ----------

    private void DoReset()
    {
        IWin32Window? owner = Visible ? this : null;
        var answer = MessageBox.Show(
            owner,
            "آیا مصرف Chrome از صفر محاسبه شود؟",
            "PTX Chrome Monitor",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

        if (answer != DialogResult.Yes) return;

        _net.Reset();
        _alert.AlertTriggered = false;
        _last = _net.Snapshot();
        SaveUsage();
        UpdateUi();
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_settings.Clone());
        var result = Visible ? dialog.ShowDialog(this) : dialog.ShowDialog();
        if (result != DialogResult.OK) return;

        var updated = dialog.Result;

        // Changing the limit re-arms the alert (a new limit is a new decision by the user).
        if (updated.ThresholdBytes != _settings.ThresholdBytes)
            _alert.AlertTriggered = false;

        bool autoStartChanged = updated.AutoStart != _settings.AutoStart;
        updated.AlertsEnabled = _settings.AlertsEnabled;
        _settings = updated;
        _store.SaveSettings(_settings);
        SaveUsage();

        if (autoStartChanged)
        {
            try
            {
                _store.SetAutoStart(_settings.AutoStart);
            }
            catch (Exception ex)
            {
                MessageBox.Show("تنظیم اجرای خودکار انجام نشد:\n" + ex.Message, "PTX Chrome Monitor");
            }
        }

        _miAlerts.Checked = _settings.AlertsEnabled;
        UpdateUi();
    }

    private void SaveUsage() => _store.SaveUsage(new UsageData
    {
        DownloadBytes = _net.DownloadBytes,
        UploadBytes = _net.UploadBytes,
        AlertTriggered = _alert.AlertTriggered,
    });

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _timer.Stop();
        SaveUsage();
        _net.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        base.OnFormClosing(e);
    }
}
