namespace ScreenAlarm;

internal sealed class AlarmOverlayForm : Form
{
    private readonly GifPlayerControl gifPlayer;
    private readonly Action<Size> saveWindowSize;
    private readonly Action<Point> saveWindowLocation;
    private readonly Action<Rectangle> saveGifBounds;
    private readonly Action<Rectangle> saveBubbleBounds;
    private readonly Action<Rectangle> saveTextBounds;
    private readonly System.Windows.Forms.Timer hideHandlesTimer = new() { Interval = 2000 };
    private Rectangle bubbleBounds;
    private Rectangle textBounds;
    private Rectangle gifBounds;
    private readonly Size initialWindowSize;
    private Point dragStartScreen;
    private Point formStart;
    private Rectangle boundsAtDragStart;
    private Size windowSizeAtDragStart;
    private DragMode dragMode;
    private bool handlesEnabled;
    private bool showResizeHandles;
    private bool hideDecorations;
    private const int ResizeGrip = 30;
    private const int VisibleHandleSize = 14;

    public AlarmOverlayForm(
        string gifPath,
        string speechBubblePath,
        Action openResourceSettings,
        Action toggleAlarm,
        Func<bool> isAlarmEnabled,
        Func<TimeSpan> getAlarmTime,
        Action<TimeSpan> setAlarmTime,
        Func<double> getPlaybackSpeed,
        Action<double> setPlaybackSpeed,
        Func<Size?> getWindowSize,
        Action<Size> saveWindowSize,
        Func<Point?> getWindowLocation,
        Action<Point> saveWindowLocation,
        Func<Rectangle?> getGifBounds,
        Action<Rectangle> saveGifBounds,
        Func<Rectangle?> getBubbleBounds,
        Action<Rectangle> saveBubbleBounds,
        Func<Rectangle?> getTextBounds,
        Action<Rectangle> saveTextBounds,
        Func<bool> getShowResizeHandles,
        Action<bool> setShowResizeHandles,
        Action previewAlarm,
        Action stopAlarm,
        Func<bool> isAlarmActive,
        Func<DateTime?> getAlarmDate,
        Action<DateTime?> setAlarmDate,
        Action exitApplication)
    {
        this.saveWindowSize = saveWindowSize;
        this.saveWindowLocation = saveWindowLocation;
        this.saveGifBounds = saveGifBounds;
        this.saveBubbleBounds = saveBubbleBounds;
        this.saveTextBounds = saveTextBounds;
        handlesEnabled = getShowResizeHandles();
        gifPlayer = new GifPlayerControl(gifPath, speechBubblePath)
        {
            Dock = DockStyle.Fill,
            PlaybackSpeed = getPlaybackSpeed()
        };

        var scale = Math.Min(1d, Math.Min(360d / gifPlayer.GifSize.Width, 280d / gifPlayer.GifSize.Height));
        var defaultSize = gifPlayer.HasSpeechBubble
            ? new Size(360, 260)
            : new Size(
                Math.Max(150, (int)Math.Round(gifPlayer.GifSize.Width * scale)),
                Math.Max(100, (int)Math.Round(gifPlayer.GifSize.Height * scale)));
        var savedSize = getWindowSize();

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        var minimumSize = new Size(80, 60);
        ClientSize = savedSize is { Width: > 0, Height: > 0 } size ? size : defaultSize;
        initialWindowSize = ClientSize;
        MinimumSize = minimumSize;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;

        gifBounds = getGifBounds() ?? gifPlayer.GetGifBounds(ClientSize);
        bubbleBounds = getBubbleBounds() ?? gifPlayer.GetDefaultBubbleBounds(ClientSize);
        textBounds = getTextBounds() ?? GifPlayerControl.GetDefaultTextBounds(bubbleBounds);

        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
        Location = getWindowLocation() ?? new Point(
            workingArea.Right - Width - 20,
            workingArea.Top + 20);

        gifPlayer.FrameChanged += (_, _) => RefreshLayeredWindow();

        var menu = new ContextMenuStrip();
        var currentSummary = new ToolStripLabel
        {
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        var timeLabel = new ToolStripLabel("알람 시간")
        {
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        var hourInput = CreateTimeInput(0);
        var minuteInput = CreateTimeInput(1);
        hourInput.SetTabTargets(minuteInput, null);
        minuteInput.SetTabTargets(null, hourInput);
        var colon = new Label
        {
            Text = ":",
            AutoSize = false,
            Size = new Size(14, 24),
            TextAlign = ContentAlignment.MiddleCenter
        };
        var timePanel = new FlowLayoutPanel
        {
            AutoSize = false,
            Size = new Size(150, 28),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = SystemColors.Window
        };
        timePanel.Controls.Add(hourInput);
        timePanel.Controls.Add(colon);
        timePanel.Controls.Add(minuteInput);

        var timeHost = new ToolStripControlHost(timePanel)
        {
            AutoSize = false,
            Size = new Size(166, 32),
            Margin = new Padding(8, 2, 8, 5)
        };
        var syncingTime = false;
        var timeDirty = false;
        void CommitTime()
        {
            if (syncingTime || !timeDirty || !TryReadTime(hourInput, 23, out var hour) ||
                !TryReadTime(minuteInput, 59, out var minute))
                return;

            timeDirty = false;
            setAlarmTime(new TimeSpan(hour, minute, 0));
        }
        hourInput.TextChanged += (_, _) =>
        {
            if (!syncingTime)
                timeDirty = true;
            if (!syncingTime && hourInput.TextLength == 2)
            {
                minuteInput.Select();
                minuteInput.SelectAll();
            }
        };
        minuteInput.TextChanged += (_, _) =>
        {
            if (!syncingTime)
                timeDirty = true;
        };
        hourInput.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitTime();
                e.SuppressKeyPress = true;
            }
        };
        minuteInput.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitTime();
                e.SuppressKeyPress = true;
            }
        };

        var dateMenu = new ToolStripMenuItem();
        var dailyDateItem = new ToolStripMenuItem("매일 반복");
        var todayDateItem = new ToolStripMenuItem("오늘 한 번");
        var tomorrowDateItem = new ToolStripMenuItem("내일 한 번");
        dailyDateItem.Click += (_, _) => setAlarmDate(null);
        todayDateItem.Click += (_, _) => setAlarmDate(DateTime.Today);
        tomorrowDateItem.Click += (_, _) => setAlarmDate(DateTime.Today.AddDays(1));

        var datePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd",
            Width = 150,
            MinDate = DateTime.Today
        };
        var dateHost = new ToolStripControlHost(datePicker)
        {
            AutoSize = false,
            Size = new Size(170, 28),
            Margin = new Padding(8, 2, 8, 5)
        };
        var syncingDate = false;
        datePicker.ValueChanged += (_, _) =>
        {
            if (!syncingDate)
                setAlarmDate(datePicker.Value.Date);
        };
        dateMenu.DropDownItems.AddRange([
            dailyDateItem,
            todayDateItem,
            tomorrowDateItem,
            new ToolStripSeparator(),
            new ToolStripLabel("직접 날짜 선택"),
            dateHost
        ]);

        var speedLabel = new ToolStripLabel("GIF 재생 속도 (배)")
        {
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        var speedInput = new NumericUpDown
        {
            DecimalPlaces = 2,
            Minimum = 0.1m,
            Maximum = 5m,
            Increment = 0.1m,
            Width = 130,
            TextAlign = HorizontalAlignment.Center
        };
        var speedHost = new ToolStripControlHost(speedInput)
        {
            AutoSize = false,
            Size = new Size(150, 28),
            Margin = new Padding(8, 2, 8, 5)
        };
        var syncingSpeed = false;
        speedInput.ValueChanged += (_, _) =>
        {
            if (syncingSpeed)
                return;

            var speed = (double)speedInput.Value;
            gifPlayer.PlaybackSpeed = speed;
            setPlaybackSpeed(speed);
        };

        var resourcesItem = new ToolStripMenuItem("리소스 설정...");
        resourcesItem.Click += (_, _) => openResourceSettings();

        var previewAlarmItem = new ToolStripMenuItem("알람 미리보기");
        previewAlarmItem.Click += (_, _) => previewAlarm();

        var stopAlarmItem = new ToolStripMenuItem("알람 중지");
        stopAlarmItem.Click += (_, _) => stopAlarm();

        var enabledItem = new ToolStripMenuItem("카운트다운 알람 사용");
        enabledItem.Click += (_, _) => toggleAlarm();

        var resetLayoutItem = new ToolStripMenuItem("배치 초기화");
        resetLayoutItem.Click += (_, _) =>
        {
            bubbleBounds = gifPlayer.GetDefaultBubbleBounds(ClientSize);
            textBounds = GifPlayerControl.GetDefaultTextBounds(bubbleBounds);
            gifBounds = gifPlayer.GetGifBounds(ClientSize);
            saveGifBounds(gifBounds);
            saveBubbleBounds(bubbleBounds);
            saveTextBounds(textBounds);
            RefreshLayeredWindow();
        };

        var handlesItem = new ToolStripMenuItem("크기 조절 마크 표시");
        handlesItem.Click += (_, _) =>
        {
            handlesEnabled = !handlesEnabled;
            if (!handlesEnabled)
            {
                hideHandlesTimer.Stop();
                showResizeHandles = false;
            }
            setShowResizeHandles(handlesEnabled);
            RefreshLayeredWindow();
        };

        var closeItem = new ToolStripMenuItem("알람 화면 끄기");
        closeItem.Click += (_, _) => Close();

        var exitItem = new ToolStripMenuItem("프로그램 종료");
        exitItem.Click += (_, _) => exitApplication();

        menu.Items.AddRange([
            currentSummary,
            new ToolStripSeparator(),
            timeLabel,
            timeHost,
            dateMenu,
            new ToolStripSeparator(),
            speedLabel,
            speedHost,
            new ToolStripSeparator(),
            resourcesItem,
            previewAlarmItem,
            stopAlarmItem,
            new ToolStripSeparator(),
            enabledItem,
            resetLayoutItem,
            handlesItem,
            new ToolStripSeparator(),
            closeItem,
            exitItem
        ]);
        menu.Opening += (_, _) =>
        {
            syncingTime = true;
            var alarmTime = getAlarmTime();
            hourInput.Text = alarmTime.Hours.ToString("00");
            minuteInput.Text = alarmTime.Minutes.ToString("00");
            syncingTime = false;
            syncingSpeed = true;
            speedInput.Value = Math.Clamp((decimal)getPlaybackSpeed(), speedInput.Minimum, speedInput.Maximum);
            syncingSpeed = false;
            enabledItem.Checked = isAlarmEnabled();
            handlesItem.Checked = handlesEnabled;
            var alarmDate = getAlarmDate();
            var summaryDate = alarmDate.HasValue
                ? alarmDate.Value.ToString("yyyy-MM-dd")
                : "매일";
            currentSummary.Text = $"현재: {summaryDate} {getAlarmTime():hh\\:mm} · {getPlaybackSpeed():0.##}×";
            stopAlarmItem.Enabled = isAlarmActive();
            syncingDate = true;
            dateMenu.Text = alarmDate.HasValue
                ? $"알람 날짜: {alarmDate.Value:yyyy-MM-dd}"
                : "알람 날짜: 매일";
            dailyDateItem.Checked = !alarmDate.HasValue;
            todayDateItem.Checked = alarmDate?.Date == DateTime.Today;
            tomorrowDateItem.Checked = alarmDate?.Date == DateTime.Today.AddDays(1);
            datePicker.Value = alarmDate.HasValue && alarmDate.Value.Date >= DateTime.Today
                ? alarmDate.Value.Date
                : DateTime.Today;
            syncingDate = false;
        };
        ContextMenuStrip = menu;
        menu.Closing += (_, _) => CommitTime();
        AttachMouseHandlers(this);
        hideHandlesTimer.Tick += (_, _) =>
        {
            hideHandlesTimer.Stop();
            if (dragMode != DragMode.None)
                return;

            showResizeHandles = false;
            RefreshLayeredWindow();
        };
        MouseEnter += (_, _) => hideHandlesTimer.Stop();
        MouseLeave += (_, _) =>
        {
            if (!handlesEnabled || dragMode != DragMode.None || !showResizeHandles)
                return;

            hideHandlesTimer.Stop();
            hideHandlesTimer.Start();
        };

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        };
        FormClosed += (_, _) =>
        {
            saveWindowSize(ClientSize);
            saveWindowLocation(Location);
            saveGifBounds(gifBounds);
            saveBubbleBounds(bubbleBounds);
            saveTextBounds(textBounds);
            hideHandlesTimer.Dispose();
            menu.Dispose();
            gifPlayer.Dispose();
        };
    }

    public void UpdateCountdown(string text)
    {
        gifPlayer.CountdownText = text;
    }

    public bool ChangeDisplayedGif(string gifPath)
    {
        var changed = gifPlayer.ChangeGif(gifPath);
        if (changed)
            RefreshLayeredWindow();
        return changed;
    }

    public void SetFallbackAlarmMode(bool active, double normalSpeed)
    {
        hideDecorations = active;
        gifPlayer.PlaybackSpeed = active ? 3d : normalSpeed;
        RefreshLayeredWindow();
    }

    private static TimeTextBox CreateTimeInput(int tabIndex)
    {
        return new TimeTextBox
        {
            Width = 60,
            Height = 24,
            TextAlign = HorizontalAlignment.Center,
            TabIndex = tabIndex,
            Margin = Padding.Empty,
            MaxLength = 2
        };
    }

    private static bool TryReadTime(TextBox input, int maximum, out int value)
    {
        return int.TryParse(input.Text, out value) && value >= 0 && value <= maximum;
    }

    private sealed class TimeTextBox : TextBox
    {
        private Control? nextTabControl;
        private Control? previousTabControl;

        public void SetTabTargets(Control? next, Control? previous)
        {
            nextTabControl = next;
            previousTabControl = previous;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Tab && nextTabControl is not null)
            {
                nextTabControl.Select();
                return true;
            }

            if (keyData == (Keys.Shift | Keys.Tab) && previousTabControl is not null)
            {
                previousTabControl.Select();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    private void AttachMouseHandlers(Control control)
    {
        control.MouseDown += BeginInteraction;
        control.MouseMove += ContinueInteraction;
        control.MouseUp += EndInteraction;
    }

    private void BeginInteraction(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        dragStartScreen = PointToScreen(e.Location);
        formStart = Location;
        windowSizeAtDragStart = ClientSize;

        if (IsResizeGrip(new Rectangle(Point.Empty, ClientSize), e.Location))
            dragMode = DragMode.WindowResize;
        else if (IsResizeGrip(textBounds, e.Location))
            StartComponentDrag(DragMode.TextResize, textBounds);
        else if (IsResizeGrip(bubbleBounds, e.Location))
            StartComponentDrag(DragMode.BubbleResize, bubbleBounds);
        else if (IsResizeGrip(gifBounds, e.Location))
            StartComponentDrag(DragMode.GifResize, gifBounds);
        else if (textBounds.Contains(e.Location))
            StartComponentDrag(DragMode.TextMove, textBounds);
        else if (bubbleBounds.Contains(e.Location))
            StartComponentDrag(DragMode.BubbleMove, bubbleBounds);
        else if (gifBounds.Contains(e.Location))
            dragMode = DragMode.WholeWindowMove;

        if (dragMode != DragMode.None)
            Capture = true;
    }

    private void StartComponentDrag(DragMode mode, Rectangle bounds)
    {
        dragMode = mode;
        boundsAtDragStart = bounds;
    }

    private void ContinueInteraction(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            if (handlesEnabled)
            {
                hideHandlesTimer.Stop();
                var shouldShowHandles = gifBounds.Contains(e.Location) ||
                                        bubbleBounds.Contains(e.Location) ||
                                        textBounds.Contains(e.Location);
                if (!showResizeHandles && shouldShowHandles)
                {
                    showResizeHandles = true;
                    RefreshLayeredWindow();
                }
            }
            Cursor = CursorForPoint(e.Location);
            return;
        }

        var screenPoint = PointToScreen(e.Location);
        var deltaX = screenPoint.X - dragStartScreen.X;
        var deltaY = screenPoint.Y - dragStartScreen.Y;

        switch (dragMode)
        {
            case DragMode.WholeWindowMove:
                Location = new Point(formStart.X + deltaX, formStart.Y + deltaY);
                break;
            case DragMode.WindowResize:
                ClientSize = new Size(
                    Math.Max(MinimumSize.Width, windowSizeAtDragStart.Width + deltaX),
                    Math.Max(MinimumSize.Height, windowSizeAtDragStart.Height + deltaY));
                saveWindowSize(ClientSize);
                break;
            case DragMode.BubbleMove:
                bubbleBounds = MoveFreely(boundsAtDragStart, deltaX, deltaY);
                ExpandCanvasToFit(bubbleBounds);
                RefreshLayeredWindow();
                break;
            case DragMode.TextMove:
                textBounds = MoveFreely(boundsAtDragStart, deltaX, deltaY);
                ExpandCanvasToFit(textBounds);
                RefreshLayeredWindow();
                break;
            case DragMode.GifResize:
                gifBounds = ResizeWithAspect(boundsAtDragStart, deltaX, 60, 40);
                ExpandCanvasToFit(gifBounds);
                RefreshLayeredWindow();
                break;
            case DragMode.BubbleResize:
                bubbleBounds = ResizeWithAspect(boundsAtDragStart, deltaX, 50, 40);
                ExpandCanvasToFit(bubbleBounds);
                RefreshLayeredWindow();
                break;
            case DragMode.TextResize:
                textBounds = new Rectangle(boundsAtDragStart.Location,
                    new Size(Math.Max(60, boundsAtDragStart.Width + deltaX),
                        Math.Max(24, boundsAtDragStart.Height + deltaY)));
                ExpandCanvasToFit(textBounds);
                RefreshLayeredWindow();
                break;
        }
    }

    private void EndInteraction(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        Capture = false;

        switch (dragMode)
        {
            case DragMode.WholeWindowMove:
                saveWindowLocation(Location);
                break;
            case DragMode.WindowResize:
                saveWindowSize(ClientSize);
                break;
            case DragMode.BubbleMove:
            case DragMode.BubbleResize:
                saveBubbleBounds(bubbleBounds);
                saveWindowSize(ClientSize);
                break;
            case DragMode.TextMove:
            case DragMode.TextResize:
                saveTextBounds(textBounds);
                saveWindowSize(ClientSize);
                break;
            case DragMode.GifResize:
                saveGifBounds(gifBounds);
                saveWindowSize(ClientSize);
                break;
        }

        dragMode = DragMode.None;
        if (handlesEnabled)
        {
            showResizeHandles = true;
            RefreshLayeredWindow();
        }
    }

    private static Rectangle MoveFreely(Rectangle original, int deltaX, int deltaY)
    {
        var x = Math.Max(0, original.X + deltaX);
        var y = Math.Max(0, original.Y + deltaY);
        return new Rectangle(x, y, original.Width, original.Height);
    }

    private static Rectangle ResizeWithAspect(Rectangle original, int deltaX, int minimumWidth, int minimumHeight)
    {
        var width = Math.Max(minimumWidth, original.Width + deltaX);
        var height = Math.Max(minimumHeight,
            (int)Math.Round(width * (double)original.Height / Math.Max(1, original.Width)));
        return new Rectangle(original.Location, new Size(width, height));
    }

    private void ExpandCanvasToFit(Rectangle component)
    {
        var width = Math.Max(ClientSize.Width, component.Right + 12);
        var height = Math.Max(ClientSize.Height, component.Bottom + 12);
        if (width != ClientSize.Width || height != ClientSize.Height)
            ClientSize = new Size(width, height);
    }

    private Cursor CursorForPoint(Point point)
    {
        if (IsResizeGrip(new Rectangle(Point.Empty, ClientSize), point) ||
            IsResizeGrip(textBounds, point) || IsResizeGrip(bubbleBounds, point) ||
            IsResizeGrip(gifBounds, point))
            return Cursors.SizeNWSE;

        if (textBounds.Contains(point) || bubbleBounds.Contains(point) ||
            gifBounds.Contains(point))
            return Cursors.SizeAll;

        return Cursors.Default;
    }

    private static bool IsResizeGrip(Rectangle bounds, Point point)
    {
        return new Rectangle(bounds.Right - ResizeGrip, bounds.Bottom - ResizeGrip,
            ResizeGrip, ResizeGrip).Contains(point);
    }

    private void RefreshLayeredWindow()
    {
        LayeredWindowRenderer.Update(this, graphics =>
        {
            // 거의 보이지 않는 알파 영역으로 투명 부분에서도 드래그와 크기 조절을 받는다.
            using var hitBrush = new SolidBrush(Color.FromArgb(1, 0, 0, 0));
            graphics.FillRectangle(hitBrush, gifBounds);
            graphics.FillRectangle(hitBrush, bubbleBounds);
            graphics.FillRectangle(hitBrush, textBounds);
            graphics.FillRectangle(hitBrush, ClientSize.Width - ResizeGrip,
                ClientSize.Height - ResizeGrip, ResizeGrip, ResizeGrip);
            gifPlayer.Render(graphics, ClientSize, gifBounds, bubbleBounds, textBounds, hideDecorations);

            if (handlesEnabled && (showResizeHandles || dragMode != DragMode.None))
            {
                DrawResizeHandle(graphics, gifBounds, Color.DeepSkyBlue);
                DrawResizeHandle(graphics, bubbleBounds, Color.Orange);
                DrawResizeHandle(graphics, textBounds, Color.LimeGreen);
                DrawResizeHandle(graphics, new Rectangle(Point.Empty, ClientSize), Color.White);
            }
        });
    }

    private static void DrawResizeHandle(Graphics graphics, Rectangle bounds, Color color)
    {
        var handle = new Rectangle(
            bounds.Right - VisibleHandleSize,
            bounds.Bottom - VisibleHandleSize,
            VisibleHandleSize,
            VisibleHandleSize);
        using var fill = new SolidBrush(Color.FromArgb(230, color));
        using var outline = new Pen(Color.FromArgb(230, Color.Black), 2f);
        graphics.FillEllipse(fill, handle);
        graphics.DrawEllipse(outline, handle);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= LayeredWindowRenderer.ExtendedStyle;
            return parameters;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RefreshLayeredWindow();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // 핸들 생성 시 모니터 DPI가 적용된 뒤 저장된 픽셀 크기를 최종 복원한다.
        ClientSize = initialWindowSize;
        Activate();
        BringToFront();
        RefreshLayeredWindow();
    }

    private enum DragMode
    {
        None,
        WholeWindowMove,
        WindowResize,
        BubbleMove,
        BubbleResize,
        GifResize,
        TextMove,
        TextResize
    }
}
