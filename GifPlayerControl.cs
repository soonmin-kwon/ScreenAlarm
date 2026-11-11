using System.Drawing.Imaging;
using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ScreenAlarm;

internal sealed class GifPlayerControl : Control
{
    private Image image;
    private readonly Bitmap? speechBubble;
    private FrameDimension frameDimension;
    private int[] frameDelays;
    private readonly System.Windows.Forms.Timer frameTimer = new();
    private int frameIndex;
    private double playbackSpeed = 1d;
    private string countdownText = string.Empty;

    public event EventHandler? FrameChanged;

    public GifPlayerControl(string gifPath, string? speechBubblePath = null)
    {
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        image = LoadGifOrFallback(gifPath);
        if (!string.IsNullOrWhiteSpace(speechBubblePath) && File.Exists(speechBubblePath))
        {
            using var sourceBubble = new Bitmap(speechBubblePath);
            speechBubble = new Bitmap(sourceBubble);
        }
        frameDimension = new FrameDimension(image.FrameDimensionsList[0]);
        var frameCount = image.GetFrameCount(frameDimension);
        frameDelays = ReadFrameDelays(image, frameCount);

        BackColor = Color.Magenta;
        frameTimer.Tick += AdvanceFrame;
        if (frameCount > 1)
        {
            SetTimerInterval();
            frameTimer.Start();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Size GifSize => image.Size;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HasSpeechBubble => speechBubble is not null;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double PlaybackSpeed
    {
        get => playbackSpeed;
        set
        {
            playbackSpeed = Math.Clamp(value, 0.1d, 5d);
            SetTimerInterval();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string CountdownText
    {
        get => countdownText;
        set
        {
            countdownText = value;
            Invalidate();
            FrameChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool ChangeGif(string gifPath)
    {
        if (!string.IsNullOrWhiteSpace(gifPath) && !File.Exists(gifPath))
            return false;

        Image? nextImage = null;
        try
        {
            nextImage = LoadGifOrFallback(gifPath);
            var nextDimension = new FrameDimension(nextImage.FrameDimensionsList[0]);
            var nextDelays = ReadFrameDelays(nextImage, nextImage.GetFrameCount(nextDimension));

            frameTimer.Stop();
            image.Dispose();
            image = nextImage;
            frameDimension = nextDimension;
            frameDelays = nextDelays;
            frameIndex = 0;
            nextImage = null;

            if (frameDelays.Length > 1)
            {
                SetTimerInterval();
                frameTimer.Start();
            }

            FrameChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch
        {
            nextImage?.Dispose();
            return false;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Render(e.Graphics, ClientSize, null, null, null);
    }

    public void Render(Graphics graphics, Size targetSize, Rectangle? gifOverride,
        Rectangle? bubbleOverride, Rectangle? textOverride, bool hideDecorations = false)
    {
        if (targetSize.Width <= 0 || targetSize.Height <= 0)
            return;

        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        Rectangle textBounds;
        if (hideDecorations)
        {
            var gifBounds = gifOverride ?? GetGifBounds(targetSize);
            graphics.DrawImage(image, FitInside(image.Size, gifBounds));
            return;
        }

        if (speechBubble is not null)
        {
            var gifBounds = gifOverride ?? GetGifBounds(targetSize);
            var bubbleBounds = bubbleOverride ?? GetDefaultBubbleBounds(targetSize);

            graphics.DrawImage(image, gifBounds);
            graphics.DrawImage(speechBubble, bubbleBounds);

            textBounds = textOverride ?? GetDefaultTextBounds(bubbleBounds);
        }
        else
        {
            var gifBounds = gifOverride ?? FitInside(image.Size, new Rectangle(Point.Empty, targetSize));
            graphics.DrawImage(image, gifBounds);
            textBounds = new Rectangle(4, Math.Max(0, targetSize.Height - 38),
                Math.Max(1, targetSize.Width - 8), 34);
        }

        if (!string.IsNullOrWhiteSpace(countdownText))
        {
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            var fontSize = Math.Clamp(textBounds.Height * 0.38f, 8f, 36f);
            using var countdownFont = new Font("Segoe UI", fontSize, FontStyle.Bold);
            using var shadowBrush = new SolidBrush(Color.Black);
            using var textBrush = new SolidBrush(Color.White);
            foreach (var offset in new[] { new Point(-1, 0), new Point(1, 0), new Point(0, -1), new Point(0, 1) })
            {
                var shadowBounds = textBounds;
                shadowBounds.Offset(offset);
                graphics.DrawString(countdownText, countdownFont, shadowBrush, shadowBounds, format);
            }
            graphics.DrawString(countdownText, countdownFont, textBrush, textBounds, format);
        }
    }

    public Rectangle GetGifBounds(Size targetSize)
    {
        return speechBubble is not null
            ? FitInside(image.Size,
                new Rectangle(0, targetSize.Height / 3, targetSize.Width * 13 / 20, targetSize.Height * 2 / 3))
            : FitInside(image.Size, new Rectangle(Point.Empty, targetSize));
    }

    public Rectangle GetDefaultBubbleBounds(Size targetSize)
    {
        if (speechBubble is null)
            return Rectangle.Empty;

        return FitInside(speechBubble.Size,
            new Rectangle(targetSize.Width * 2 / 5, targetSize.Height / 25,
                targetSize.Width * 29 / 50, targetSize.Height * 29 / 50));
    }

    public static Rectangle GetDefaultTextBounds(Rectangle bubbleBounds)
    {
        return new Rectangle(
            bubbleBounds.X + bubbleBounds.Width * 16 / 100,
            bubbleBounds.Y + bubbleBounds.Height * 12 / 100,
            bubbleBounds.Width * 72 / 100,
            bubbleBounds.Height * 58 / 100);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            frameTimer.Dispose();
            speechBubble?.Dispose();
            image.Dispose();
        }

        base.Dispose(disposing);
    }

    private void AdvanceFrame(object? sender, EventArgs e)
    {
        frameIndex = (frameIndex + 1) % frameDelays.Length;
        image.SelectActiveFrame(frameDimension, frameIndex);
        Invalidate();
        FrameChanged?.Invoke(this, EventArgs.Empty);
        SetTimerInterval();
    }

    private void SetTimerInterval()
    {
        if (frameDelays.Length == 0)
            return;

        frameTimer.Interval = Math.Max(10,
            (int)Math.Round(frameDelays[frameIndex] / playbackSpeed));
    }

    private static int[] ReadFrameDelays(Image source, int frameCount)
    {
        var delays = Enumerable.Repeat(100, frameCount).ToArray();

        try
        {
            const int frameDelayPropertyId = 0x5100;
            var raw = source.GetPropertyItem(frameDelayPropertyId)?.Value;
            if (raw is null)
                return delays;

            for (var i = 0; i < frameCount && i * 4 + 3 < raw.Length; i++)
            {
                var hundredths = BitConverter.ToInt32(raw, i * 4);
                delays[i] = Math.Max(20, hundredths * 10);
            }
        }
        catch (ArgumentException)
        {
            // 프레임 지연 정보가 없는 이미지는 기본 100ms로 재생한다.
        }

        return delays;
    }

    private static Image LoadGifOrFallback(string gifPath)
    {
        return !string.IsNullOrWhiteSpace(gifPath) && File.Exists(gifPath)
            ? Image.FromFile(gifPath)
            : CreateFallbackClock();
    }

    private static Bitmap CreateFallbackClock()
    {
        var bitmap = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var faceBrush = new SolidBrush(Color.FromArgb(255, 255, 247, 220));
        using var rimPen = new Pen(Color.FromArgb(255, 55, 48, 58), 12f);
        using var accentPen = new Pen(Color.FromArgb(255, 244, 126, 160), 8f);
        using var handPen = new Pen(Color.FromArgb(255, 55, 48, 58), 9f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var eyeBrush = new SolidBrush(Color.FromArgb(255, 55, 48, 58));

        graphics.FillEllipse(faceBrush, 24, 24, 208, 208);
        graphics.DrawEllipse(rimPen, 24, 24, 208, 208);
        graphics.DrawArc(accentPen, 38, 38, 180, 180, 205, 130);

        graphics.FillEllipse(eyeBrush, 81, 94, 14, 18);
        graphics.FillEllipse(eyeBrush, 161, 94, 14, 18);
        graphics.DrawArc(rimPen, 92, 112, 72, 58, 20, 140);

        graphics.DrawLine(handPen, 128, 128, 128, 66);
        graphics.DrawLine(handPen, 128, 128, 172, 149);
        graphics.FillEllipse(eyeBrush, 119, 119, 18, 18);
        return bitmap;
    }

    private static Rectangle FitInside(Size source, Rectangle area)
    {
        var scale = Math.Min((double)area.Width / source.Width, (double)area.Height / source.Height);
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        return new Rectangle(
            area.X + (area.Width - width) / 2,
            area.Y + (area.Height - height) / 2,
            width,
            height);
    }

}
