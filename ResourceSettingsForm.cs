using System.Drawing.Text;

namespace ScreenAlarm;

internal sealed class ResourceSettingsForm : Form
{
    private readonly TextBox mainGifBox = CreatePathBox();
    private readonly TextBox speechBubbleBox = CreatePathBox();
    private readonly TextBox alarmGifBox = CreatePathBox();
    private readonly TextBox normalSoundBox = CreatePathBox();
    private readonly TextBox alarmSoundBox = CreatePathBox();
    private readonly TextBox countdownFontBox = CreatePathBox();
    private readonly TextBox countdownFontPathBox = CreatePathBox();
    private readonly TextBox countdownTextBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox alarmTextBox = new() { Dock = DockStyle.Fill };
    private Font countdownFont;
    private Font? countdownFontPreview;
    private PrivateFontCollection? customFontCollection;

    public ResourceSettingsForm(string mainGif, string speechBubble, string normalSound,
        string alarmGif, string alarmSound, string countdownFontName,
        string countdownFontPath, float countdownFontSize, FontStyle countdownFontStyle,
        string countdownTextFormat, string alarmText)
    {
        Text = "Screen Alarm - 리소스 설정";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(820, 566);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleMode = AutoScaleMode.Dpi;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 10F);
        TopMost = true;

        mainGifBox.Text = mainGif;
        speechBubbleBox.Text = speechBubble;
        normalSoundBox.Text = normalSound;
        alarmGifBox.Text = alarmGif;
        alarmSoundBox.Text = alarmSound;
        countdownFont = CreateFont(countdownFontName, countdownFontSize, countdownFontStyle);
        UpdateFontSummary();
        if (!string.IsNullOrWhiteSpace(countdownFontPath))
            TrySetCustomFont(countdownFontPath, showError: false);
        countdownTextBox.Text = string.IsNullOrWhiteSpace(countdownTextFormat)
            ? "{time} 남음"
            : countdownTextFormat;
        alarmTextBox.Text = string.IsNullOrWhiteSpace(alarmText) ? "알람 시간!" : alarmText;

        var outerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        outerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

        var resourcesLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 3,
            RowCount = 10
        };
        resourcesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        resourcesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        resourcesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (var row = 0; row < 9; row++)
            resourcesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        resourcesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        AddResourceRow(resourcesLayout, 0, "평소 GIF", mainGifBox,
            "GIF 이미지 (*.gif)|*.gif", allowClear: true);
        AddResourceRow(resourcesLayout, 1, "말풍선 PNG", speechBubbleBox,
            "PNG 이미지 (*.png)|*.png", allowClear: true);
        AddResourceRow(resourcesLayout, 2, "평소 사운드", normalSoundBox,
            "오디오 파일 (*.wav;*.mp3;*.wma)|*.wav;*.mp3;*.wma|모든 파일 (*.*)|*.*", allowClear: true);
        AddResourceRow(resourcesLayout, 3, "알람 GIF", alarmGifBox,
            "GIF 이미지 (*.gif)|*.gif", allowClear: true);
        AddResourceRow(resourcesLayout, 4, "알람 사운드", alarmSoundBox,
            "오디오 파일 (*.wav;*.mp3;*.wma)|*.wav;*.mp3;*.wma|모든 파일 (*.*)|*.*", allowClear: true);
        AddTextRow(resourcesLayout, 5);
        AddAlarmTextRow(resourcesLayout, 6);
        AddFontFileRow(resourcesLayout, 7);
        AddFontRow(resourcesLayout, 8);

        var hint = new Label
        {
            Text = "문구 변수: {time}, {hours}, {minutes}, {totalMinutes}  ·  평소 GIF와 말풍선은 비워 둘 수 있습니다.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(3, 10, 3, 10)
        };
        resourcesLayout.Controls.Add(hint, 0, 9);
        resourcesLayout.SetColumnSpan(hint, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(18, 12, 18, 10)
        };
        var saveButton = new Button { Text = "저장", AutoSize = true, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "취소", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);
        outerLayout.Controls.Add(resourcesLayout, 0, 0);
        outerLayout.Controls.Add(buttons, 0, 1);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        Controls.Add(outerLayout);
    }

    public string MainGifPath => mainGifBox.Text.Trim();
    public string SpeechBubblePath => speechBubbleBox.Text.Trim();
    public string NormalSoundPath => normalSoundBox.Text.Trim();
    public string AlarmGifPath => alarmGifBox.Text.Trim();
    public string AlarmSoundPath => alarmSoundBox.Text.Trim();
    public string CountdownFontName => countdownFont.Name;
    public string CountdownFontPath => countdownFontPathBox.Text.Trim();
    public float CountdownFontSize => countdownFont.SizeInPoints;
    public FontStyle CountdownFontStyle => countdownFont.Style;
    public string CountdownTextFormat => string.IsNullOrWhiteSpace(countdownTextBox.Text)
        ? "{time} 남음"
        : countdownTextBox.Text;
    public string AlarmText => string.IsNullOrWhiteSpace(alarmTextBox.Text)
        ? "알람 시간!"
        : alarmTextBox.Text;

    private static TextBox CreatePathBox() => new() { ReadOnly = true, Dock = DockStyle.Fill };

    private void AddResourceRow(TableLayoutPanel layout, int row, string label,
        TextBox pathBox, string filter, bool allowClear)
    {
        layout.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 8, 12, 8)
        }, 0, row);
        layout.Controls.Add(pathBox, 1, row);

        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
        var browse = new Button { Text = "찾아보기", AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                pathBox.Text = dialog.FileName;
        };
        buttons.Controls.Add(browse);

        if (allowClear)
        {
            var clear = new Button { Text = "비우기", AutoSize = true };
            clear.Click += (_, _) => pathBox.Clear();
            buttons.Controls.Add(clear);
        }

        layout.Controls.Add(buttons, 2, row);
    }

    private void AddFontRow(TableLayoutPanel layout, int row)
    {
        layout.Controls.Add(new Label
        {
            Text = "카운트다운 글꼴",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 8, 12, 8)
        }, 0, row);
        layout.Controls.Add(countdownFontBox, 1, row);

        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
        var select = new Button { Text = "선택", AutoSize = true };
        select.Click += (_, _) =>
        {
            using var initialFont = (Font)countdownFont.Clone();
            using var dialog = new FontDialog
            {
                Font = initialFont,
                FontMustExist = customFontCollection is null,
                ShowEffects = false,
                MinSize = 6,
                MaxSize = 72
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            if (customFontCollection is not null &&
                string.Equals(dialog.Font.Name, countdownFont.Name, StringComparison.OrdinalIgnoreCase))
            {
                var family = customFontCollection.Families[0];
                var style = GetAvailableStyle(family, dialog.Font.Style);
                ReplaceFont(new Font(family, dialog.Font.SizeInPoints, style),
                    customFontCollection, countdownFontPathBox.Text);
            }
            else
            {
                ReplaceFont((Font)dialog.Font.Clone(), null, string.Empty);
            }
        };
        var reset = new Button { Text = "기본값", AutoSize = true };
        reset.Click += (_, _) =>
        {
            ReplaceFont(new Font("Segoe UI", 10F, FontStyle.Bold), null, string.Empty);
        };
        buttons.Controls.Add(select);
        buttons.Controls.Add(reset);
        layout.Controls.Add(buttons, 2, row);
    }

    private void AddFontFileRow(TableLayoutPanel layout, int row)
    {
        layout.Controls.Add(new Label
        {
            Text = "폰트 파일",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 8, 12, 8)
        }, 0, row);
        layout.Controls.Add(countdownFontPathBox, 1, row);

        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
        var browse = new Button { Text = "찾아보기", AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog
            {
                Title = "카운트다운에 사용할 폰트 파일 선택",
                Filter = "폰트 파일 (*.ttf;*.otf)|*.ttf;*.otf|모든 파일 (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                TrySetCustomFont(dialog.FileName, showError: true);
        };
        var clear = new Button { Text = "비우기", AutoSize = true };
        clear.Click += (_, _) =>
            ReplaceFont(new Font("Segoe UI", 10F, FontStyle.Bold), null, string.Empty);
        buttons.Controls.Add(browse);
        buttons.Controls.Add(clear);
        layout.Controls.Add(buttons, 2, row);
    }

    private void AddTextRow(TableLayoutPanel layout, int row)
    {
        layout.Controls.Add(new Label
        {
            Text = "카운트다운 문구",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 8, 12, 8)
        }, 0, row);
        layout.Controls.Add(countdownTextBox, 1, row);

        var reset = new Button { Text = "기본값", AutoSize = true };
        reset.Click += (_, _) => countdownTextBox.Text = "{time} 남음";
        layout.Controls.Add(reset, 2, row);
    }

    private void AddAlarmTextRow(TableLayoutPanel layout, int row)
    {
        layout.Controls.Add(new Label
        {
            Text = "알람 문구",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 8, 12, 8)
        }, 0, row);
        layout.Controls.Add(alarmTextBox, 1, row);

        var reset = new Button { Text = "기본값", AutoSize = true };
        reset.Click += (_, _) => alarmTextBox.Text = "알람 시간!";
        layout.Controls.Add(reset, 2, row);
    }

    private void UpdateFontSummary()
    {
        countdownFontBox.Text = $"{countdownFont.Name}, {countdownFont.SizeInPoints:0.#}pt, {countdownFont.Style}";
        countdownFontPreview?.Dispose();
        countdownFontPreview = new Font(countdownFont.FontFamily, 10F, countdownFont.Style);
        countdownFontBox.Font = countdownFontPreview;
    }

    private bool TrySetCustomFont(string fontPath, bool showError)
    {
        PrivateFontCollection? collection = null;
        Font? font = null;
        try
        {
            collection = new PrivateFontCollection();
            collection.AddFontFile(fontPath);
            if (collection.Families.Length == 0)
            {
                throw new ArgumentException("사용할 수 있는 글꼴이 들어 있지 않습니다.");
            }

            var family = collection.Families[0];
            var style = GetAvailableStyle(family, countdownFont.Style);
            font = new Font(family, countdownFont.SizeInPoints, style);
            ReplaceFont(font, collection, fontPath);
            font = null;
            collection = null;
            return true;
        }
        catch (Exception exception)
        {
            font?.Dispose();
            collection?.Dispose();
            if (showError)
            {
                MessageBox.Show($"폰트 파일을 불러오지 못했습니다.\n\n{exception.Message}",
                    "폰트 파일 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return false;
        }
    }

    private void ReplaceFont(Font font, PrivateFontCollection? collection, string fontPath)
    {
        countdownFontPreview?.Dispose();
        countdownFontPreview = null;
        countdownFont.Dispose();
        if (!ReferenceEquals(customFontCollection, collection))
            customFontCollection?.Dispose();
        customFontCollection = collection;
        countdownFont = font;
        countdownFontPathBox.Text = fontPath;
        UpdateFontSummary();
    }

    private static FontStyle GetAvailableStyle(FontFamily family, FontStyle requested)
    {
        if (family.IsStyleAvailable(requested))
            return requested;

        foreach (var style in new[] { FontStyle.Regular, FontStyle.Bold, FontStyle.Italic })
        {
            if (family.IsStyleAvailable(style))
                return style;
        }

        return FontStyle.Regular;
    }

    private static Font CreateFont(string fontName, float fontSize, FontStyle fontStyle)
    {
        try
        {
            return new Font(string.IsNullOrWhiteSpace(fontName) ? "Segoe UI" : fontName,
                Math.Clamp(fontSize, 6F, 72F), fontStyle);
        }
        catch (ArgumentException)
        {
            return new Font("Segoe UI", 10F, FontStyle.Bold);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            countdownFontPreview?.Dispose();
            countdownFont.Dispose();
            customFontCollection?.Dispose();
        }
        base.Dispose(disposing);
    }
}
