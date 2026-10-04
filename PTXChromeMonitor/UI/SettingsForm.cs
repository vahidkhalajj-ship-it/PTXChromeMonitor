using PTXChromeMonitor.Models;
using PTXChromeMonitor.Services;

namespace PTXChromeMonitor.UI;

public sealed class SettingsForm : Form
{
    private readonly NumericUpDown _threshold = new()
    {
        DecimalPlaces = 2,
        Minimum = 0.01m,
        Maximum = 1000000m,
        Increment = 0.5m,
        Width = 110,
        RightToLeft = RightToLeft.No,
    };

    private readonly ComboBox _unit = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 70,
        RightToLeft = RightToLeft.No,
    };

    private readonly CheckBox _sound = new() { Text = "هشدار صوتی", AutoSize = true };
    private readonly CheckBox _notification = new() { Text = "اعلان Windows", AutoSize = true };
    private readonly CheckBox _autoStart = new() { Text = "اجرای خودکار با Windows", AutoSize = true };

    public AppSettings Result { get; }

    public SettingsForm(AppSettings settings)
    {
        Result = settings;

        Text = "تنظیمات PTX Chrome Monitor";
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(360, 300);

        _unit.Items.AddRange(new object[] { "MB", "GB", "TB" });
        _unit.SelectedItem = _unit.Items.Contains(settings.Unit) ? settings.Unit : "GB";
        _threshold.Value = (decimal)Math.Clamp(settings.Threshold, 0.01, 1000000);
        _sound.Checked = settings.SoundEnabled;
        _notification.Checked = settings.NotificationEnabled;
        _autoStart.Checked = settings.AutoStart;

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(18),
        };

        panel.Controls.Add(new Label { Text = "حد هشدار:", AutoSize = true });

        var limitRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            RightToLeft = RightToLeft.No,
            Margin = new Padding(0, 4, 0, 14),
        };
        limitRow.Controls.Add(_threshold);
        limitRow.Controls.Add(_unit);
        panel.Controls.Add(limitRow);

        panel.Controls.Add(new Label { Text = "هشدارها:", AutoSize = true });
        panel.Controls.Add(_sound);
        panel.Controls.Add(_notification);

        var test = new Button { Text = "تست صدا", AutoSize = true, Margin = new Padding(3, 6, 3, 14) };
        test.Click += (_, _) => AlertService.PlaySound();
        panel.Controls.Add(test);

        panel.Controls.Add(_autoStart);

        var save = new Button
        {
            Text = "ذخیره تنظیمات",
            AutoSize = true,
            Margin = new Padding(3, 18, 3, 3),
        };
        save.Click += (_, _) =>
        {
            Result.Threshold = (double)_threshold.Value;
            Result.Unit = (string)(_unit.SelectedItem ?? "GB");
            Result.SoundEnabled = _sound.Checked;
            Result.NotificationEnabled = _notification.Checked;
            Result.AutoStart = _autoStart.Checked;
            DialogResult = DialogResult.OK;
            Close();
        };
        panel.Controls.Add(save);
        AcceptButton = save;

        Controls.Add(panel);
    }
}
