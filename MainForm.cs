using System.Media;

namespace ScreenAlarm;

// 화면에는 나타나지 않고 타이머와 트레이 아이콘의 수명만 관리한다.
internal sealed class MainForm : Form
{
    private readonly AlarmSettings settings = AlarmSettings.Load();
    private readonly System.Windows.Forms.Timer alarmTimer = new() { Interval = 500 };
    private readonly NotifyIcon trayIcon = new();
    private readonly AlarmAudioPlayer alarmAudio = new();
    private readonly AlarmAudioPlayer normalAudio = new();
    private DateTime nextAlarmAt;
    private AlarmOverlayForm? overlay;
    private bool allowExit;
    private bool startupHandled;
    private bool alarmActive;

    public MainForm()
    {
        Opacity = 0;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        Size = new Size(1, 1);

        ScheduleNextAlarm();
        alarmTimer.Tick += CheckAlarm;
        alarmTimer.Start();
        ConfigureTrayIcon();

        Shown += HandleFirstShown;
        FormClosing += HandleFormClosing;
    }

    private void ConfigureTrayIcon()
    {
        var menu = new ContextMenuStrip();
        var showGifItem = new ToolStripMenuItem("GIF 표시");
        showGifItem.Click += (_, _) => ShowAlarm(playSound: false);

        var resourcesItem = new ToolStripMenuItem("리소스 설정...");
        resourcesItem.Click += (_, _) => OpenResourceSettings();

        var previewAlarmItem = new ToolStripMenuItem("알람 미리보기");
        previewAlarmItem.Click += (_, _) => StartAlarmMedia();

        var enabledItem = new ToolStripMenuItem("카운트다운 알람 사용");
        enabledItem.Click += (_, _) => ToggleAlarm();

        var stopAlarmItem = new ToolStripMenuItem("알람 중지");
        stopAlarmItem.Click += (_, _) => StopActiveAlarm();

        var exitItem = new ToolStripMenuItem("프로그램 종료");
        exitItem.Click += (_, _) => ExitApplication();

        menu.Items.AddRange([
            showGifItem,
            resourcesItem,
            previewAlarmItem,
            enabledItem,
            stopAlarmItem,
            new ToolStripSeparator(),
            exitItem
        ]);
        menu.Opening += (_, _) =>
        {
            enabledItem.Checked = settings.Enabled;
            stopAlarmItem.Enabled = alarmActive;
        };

        trayIcon.Text = "Screen Alarm";
        trayIcon.Icon = SystemIcons.Application;
        trayIcon.ContextMenuStrip = menu;
        trayIcon.Visible = true;
        trayIcon.DoubleClick += (_, _) => ShowAlarm(playSound: false);
    }

    private void HandleFirstShown(object? sender, EventArgs e)
    {
        if (startupHandled)
            return;

        startupHandled = true;
        BeginInvoke(() =>
        {
            Hide();
            ShowAlarm(playSound: false, warnIfMissing: true);
            StartNormalSound();
        });
    }

    private void HandleFormClosing(object? sender, FormClosingEventArgs e)
    {
        settings.Save();
        if (allowExit || e.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing)
        {
            alarmAudio.Dispose();
            normalAudio.Dispose();
            trayIcon.Visible = false;
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void CheckAlarm(object? sender, EventArgs e)
    {
        UpdateCountdown();

        if (!settings.Enabled || settings.OneTimeAlarmCompleted || DateTime.Now < nextAlarmAt)
            return;

        if (settings.AlarmDate.HasValue)
        {
            settings.OneTimeAlarmCompleted = true;
            settings.Save();
        }
        else
        {
            ScheduleNextAlarm(DateTime.Now.AddSeconds(1));
        }
        StartAlarmMedia();
    }

    private void ScheduleNextAlarm(DateTime? from = null)
    {
        var reference = from ?? DateTime.Now;
        if (settings.AlarmDate.HasValue)
        {
            nextAlarmAt = settings.AlarmDate.Value.Date.Add(settings.AlarmTime);
            return;
        }

        var candidate = reference.Date.Add(settings.AlarmTime);
        nextAlarmAt = candidate > reference ? candidate : candidate.AddDays(1);
    }

    private bool ShowAlarm(bool playSound, bool warnIfMissing = true, string? overrideGifPath = null)
    {
        var gifPath = File.Exists(overrideGifPath) ? overrideGifPath : ResolveGifPath();

        if (overlay is { IsDisposed: false })
        {
            if (!overlay.ChangeDisplayedGif(gifPath))
                return false;

            overlay.Show();
            overlay.Activate();
            ApplyFallbackMode();
            UpdateCountdown();
            if (playSound)
                SystemSounds.Exclamation.Play();
            return true;
        }

        overlay = new AlarmOverlayForm(
            gifPath,
            ResolveSpeechBubblePath(),
            OpenResourceSettings,
            ToggleAlarm,
            () => settings.Enabled,
            () => settings.AlarmTime,
            SetAlarmTime,
            () => settings.PlaybackSpeed,
            SetPlaybackSpeed,
            GetWindowSize,
            SaveWindowSize,
            GetWindowLocation,
            SaveWindowLocation,
            GetGifBounds,
            SaveGifBounds,
            GetBubbleBounds,
            SaveBubbleBounds,
            GetTextBounds,
            SaveTextBounds,
            () => settings.ShowResizeHandles,
            SetShowResizeHandles,
            StartAlarmMedia,
            StopActiveAlarm,
            () => alarmActive,
            () => settings.AlarmDate,
            SetAlarmDate,
            ExitApplication);
        overlay.FormClosed += (_, _) => overlay = null;
        overlay.Show();
        overlay.Activate();
        ApplyFallbackMode();
        UpdateCountdown();

        if (playSound)
            SystemSounds.Exclamation.Play();
        return true;
    }

    private string ResolveSpeechBubblePath()
    {
        if (File.Exists(settings.SpeechBubblePath))
            return settings.SpeechBubblePath;

        return string.Empty;
    }

    private void OpenResourceSettings()
    {
        var currentMainGif = ResolveGifPath();
        var currentBubble = ResolveSpeechBubblePath();
        using var dialog = new ResourceSettingsForm(currentMainGif, currentBubble,
            settings.NormalSoundPath, settings.AlarmGifPath, settings.AlarmSoundPath);

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        if ((!string.IsNullOrWhiteSpace(dialog.MainGifPath) && !File.Exists(dialog.MainGifPath)) ||
            (!string.IsNullOrWhiteSpace(dialog.SpeechBubblePath) && !File.Exists(dialog.SpeechBubblePath)))
        {
            MessageBox.Show("입력한 평소 GIF와 말풍선 PNG 경로를 확인해 주세요.",
                "리소스 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        settings.GifPath = dialog.MainGifPath;
        settings.SpeechBubblePath = dialog.SpeechBubblePath;
        settings.NormalSoundPath = dialog.NormalSoundPath;
        settings.AlarmGifPath = dialog.AlarmGifPath;
        settings.AlarmSoundPath = dialog.AlarmSoundPath;
        settings.Save();

        overlay?.Close();
        overlay = null;
        if (alarmActive)
            StartAlarmMedia();
        else
        {
            StartNormalSound();
            ShowAlarm(playSound: false);
        }
    }

    private void ApplyFallbackMode()
    {
        var fallbackAlarmMode = alarmActive &&
                                !File.Exists(settings.AlarmGifPath) &&
                                !File.Exists(settings.AlarmSoundPath);
        overlay?.SetFallbackAlarmMode(fallbackAlarmMode, settings.PlaybackSpeed);
    }

    private void SelectAlarmGif()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "알람 시간이 되면 표시할 GIF 선택",
            Filter = "GIF 이미지 (*.gif)|*.gif",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        settings.AlarmGifPath = dialog.FileName;
        settings.Save();
    }

    private void SelectAlarmSound()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "알람 사운드 선택",
            Filter = "오디오 파일 (*.wav;*.mp3;*.wma)|*.wav;*.mp3;*.wma|모든 파일 (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        settings.AlarmSoundPath = dialog.FileName;
        settings.Save();
    }

    private void StartAlarmMedia()
    {
        alarmActive = true;
        normalAudio.Stop();
        var alarmGif = File.Exists(settings.AlarmGifPath) ? settings.AlarmGifPath : null;
        var alarmSoundExists = File.Exists(settings.AlarmSoundPath);
        if (alarmGif is not null)
            ShowAlarm(playSound: false, warnIfMissing: false, overrideGifPath: alarmGif);
        else if (overlay is not { IsDisposed: false })
            ShowAlarm(playSound: false, warnIfMissing: false);

        if (alarmGif is null && !alarmSoundExists)
            overlay?.SetFallbackAlarmMode(active: true, normalSpeed: settings.PlaybackSpeed);

        if (!alarmSoundExists || !alarmAudio.PlayOnce(settings.AlarmSoundPath))
            SystemSounds.Exclamation.Play();
        UpdateCountdown();
    }

    private void StopActiveAlarm()
    {
        alarmActive = false;
        alarmAudio.Stop();
        overlay?.SetFallbackAlarmMode(active: false, normalSpeed: settings.PlaybackSpeed);
        ShowAlarm(playSound: false, warnIfMissing: false);
        StartNormalSound();
        UpdateCountdown();
    }

    private string ResolveGifPath()
    {
        if (File.Exists(settings.GifPath))
            return settings.GifPath;

        return string.Empty;
    }

    private void ChangeGif()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "표시할 GIF 선택",
            Filter = "GIF 이미지 (*.gif)|*.gif",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        settings.GifPath = dialog.FileName;
        settings.Save();
        if (!alarmActive)
            ShowAlarm(playSound: false);
    }

    private void SetAlarmTime(TimeSpan time)
    {
        CancelActiveAlarmForReschedule();
        settings.AlarmTime = time;
        settings.Enabled = true;
        settings.OneTimeAlarmCompleted = false;
        settings.Save();
        ScheduleNextAlarm();
        UpdateCountdown();
    }

    private void SetAlarmDate(DateTime? date)
    {
        CancelActiveAlarmForReschedule();
        settings.AlarmDate = date?.Date;
        settings.Enabled = true;
        settings.OneTimeAlarmCompleted = false;
        settings.Save();
        ScheduleNextAlarm();
        UpdateCountdown();
    }

    private void ToggleAlarm()
    {
        settings.Enabled = !settings.Enabled;
        if (settings.Enabled)
            settings.OneTimeAlarmCompleted = false;
        if (!settings.Enabled)
            CancelActiveAlarmForReschedule();
        settings.Save();
        ScheduleNextAlarm();
        UpdateCountdown();
    }

    private void CancelActiveAlarmForReschedule()
    {
        if (!alarmActive)
            return;

        alarmActive = false;
        alarmAudio.Stop();
        overlay?.SetFallbackAlarmMode(active: false, normalSpeed: settings.PlaybackSpeed);
        ShowAlarm(playSound: false, warnIfMissing: false);
        StartNormalSound();
    }

    private void StartNormalSound()
    {
        normalAudio.Stop();
        if (!alarmActive && File.Exists(settings.NormalSoundPath))
            normalAudio.PlayLooping(settings.NormalSoundPath);
    }

    private void SetPlaybackSpeed(double speed)
    {
        settings.PlaybackSpeed = speed;
        settings.Save();
    }

    private Size? GetWindowSize()
    {
        return settings.WindowWidth > 0 && settings.WindowHeight > 0
            ? new Size(settings.WindowWidth, settings.WindowHeight)
            : null;
    }

    private void SaveWindowSize(Size size)
    {
        settings.WindowWidth = size.Width;
        settings.WindowHeight = size.Height;
        settings.Save();
    }

    private Point? GetWindowLocation()
    {
        return settings.WindowX != int.MinValue && settings.WindowY != int.MinValue
            ? new Point(settings.WindowX, settings.WindowY)
            : null;
    }

    private void SaveWindowLocation(Point location)
    {
        settings.WindowX = location.X;
        settings.WindowY = location.Y;
        settings.Save();
    }

    private Rectangle? GetBubbleBounds()
    {
        return settings.BubbleX >= 0 && settings.BubbleY >= 0 &&
               settings.BubbleWidth > 0 && settings.BubbleHeight > 0
            ? new Rectangle(settings.BubbleX, settings.BubbleY,
                settings.BubbleWidth, settings.BubbleHeight)
            : null;
    }

    private Rectangle? GetGifBounds()
    {
        return settings.GifX >= 0 && settings.GifY >= 0 &&
               settings.GifWidth > 0 && settings.GifHeight > 0
            ? new Rectangle(settings.GifX, settings.GifY,
                settings.GifWidth, settings.GifHeight)
            : null;
    }

    private void SaveGifBounds(Rectangle bounds)
    {
        settings.GifX = bounds.X;
        settings.GifY = bounds.Y;
        settings.GifWidth = bounds.Width;
        settings.GifHeight = bounds.Height;
        settings.Save();
    }

    private void SaveBubbleBounds(Rectangle bounds)
    {
        settings.BubbleX = bounds.X;
        settings.BubbleY = bounds.Y;
        settings.BubbleWidth = bounds.Width;
        settings.BubbleHeight = bounds.Height;
        settings.Save();
    }

    private Rectangle? GetTextBounds()
    {
        return settings.TextX >= 0 && settings.TextY >= 0 &&
               settings.TextWidth > 0 && settings.TextHeight > 0
            ? new Rectangle(settings.TextX, settings.TextY,
                settings.TextWidth, settings.TextHeight)
            : null;
    }

    private void SaveTextBounds(Rectangle bounds)
    {
        settings.TextX = bounds.X;
        settings.TextY = bounds.Y;
        settings.TextWidth = bounds.Width;
        settings.TextHeight = bounds.Height;
        settings.Save();
    }

    private void SetShowResizeHandles(bool show)
    {
        settings.ShowResizeHandles = show;
        settings.Save();
    }

    private void UpdateCountdown()
    {
        if (overlay is not { IsDisposed: false })
            return;

        if (alarmActive)
        {
            overlay.UpdateCountdown("알람 시간!");
            return;
        }

        if (!settings.Enabled)
        {
            overlay.UpdateCountdown("카운트다운 꺼짐");
            return;
        }

        if (settings.OneTimeAlarmCompleted)
        {
            overlay.UpdateCountdown("알람 완료");
            return;
        }

        var remaining = nextAlarmAt - DateTime.Now;
        var totalMinutes = Math.Max(0, (int)Math.Ceiling(remaining.TotalMinutes));
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;

        overlay.UpdateCountdown(hours > 0
            ? $"{hours}시간 {minutes}분 남음"
            : $"{minutes}분 남음");
    }

    private void ExitApplication()
    {
        allowExit = true;
        alarmAudio.Dispose();
        normalAudio.Dispose();
        trayIcon.Visible = false;
        overlay?.Close();
        Application.Exit();
    }
}
