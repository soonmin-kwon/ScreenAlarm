namespace ScreenAlarm;

internal sealed class ResourceSettingsForm : Form
{
    private readonly TextBox mainGifBox = CreatePathBox();
    private readonly TextBox speechBubbleBox = CreatePathBox();
    private readonly TextBox alarmGifBox = CreatePathBox();
    private readonly TextBox normalSoundBox = CreatePathBox();
    private readonly TextBox alarmSoundBox = CreatePathBox();

    public ResourceSettingsForm(string mainGif, string speechBubble, string normalSound,
        string alarmGif, string alarmSound)
    {
        Text = "Screen Alarm - 리소스 설정";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(760, 400);
        FormBorderStyle = FormBorderStyle.FixedDialog;
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

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 3,
            RowCount = 7
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (var row = 0; row < 5; row++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        AddResourceRow(layout, 0, "평소 GIF", mainGifBox,
            "GIF 이미지 (*.gif)|*.gif", allowClear: true);
        AddResourceRow(layout, 1, "말풍선 PNG", speechBubbleBox,
            "PNG 이미지 (*.png)|*.png", allowClear: true);
        AddResourceRow(layout, 2, "평소 사운드", normalSoundBox,
            "오디오 파일 (*.wav;*.mp3;*.wma)|*.wav;*.mp3;*.wma|모든 파일 (*.*)|*.*", allowClear: true);
        AddResourceRow(layout, 3, "알람 GIF", alarmGifBox,
            "GIF 이미지 (*.gif)|*.gif", allowClear: true);
        AddResourceRow(layout, 4, "알람 사운드", alarmSoundBox,
            "오디오 파일 (*.wav;*.mp3;*.wma)|*.wav;*.mp3;*.wma|모든 파일 (*.*)|*.*", allowClear: true);

        var hint = new Label
        {
            Text = "평소 GIF를 비우면 내장 시계가 표시됩니다. 말풍선도 비워 둘 수 있습니다.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(3, 10, 3, 10)
        };
        layout.Controls.Add(hint, 0, 5);
        layout.SetColumnSpan(hint, 3);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Anchor = AnchorStyles.Right
        };
        var saveButton = new Button { Text = "저장", AutoSize = true, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "취소", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);
        layout.Controls.Add(buttons, 1, 6);
        layout.SetColumnSpan(buttons, 2);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        Controls.Add(layout);
    }

    public string MainGifPath => mainGifBox.Text.Trim();
    public string SpeechBubblePath => speechBubbleBox.Text.Trim();
    public string NormalSoundPath => normalSoundBox.Text.Trim();
    public string AlarmGifPath => alarmGifBox.Text.Trim();
    public string AlarmSoundPath => alarmSoundBox.Text.Trim();

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
}
