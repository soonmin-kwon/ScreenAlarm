using System.Text.Json;

namespace ScreenAlarm;

internal sealed class AlarmSettings
{
    public string GifPath { get; set; } = string.Empty;
    public TimeSpan AlarmTime { get; set; } = new(8, 0, 0);
    public bool Enabled { get; set; } = true;
    public double PlaybackSpeed { get; set; } = 1d;
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
    public int WindowX { get; set; } = int.MinValue;
    public int WindowY { get; set; } = int.MinValue;
    public int BubbleX { get; set; } = -1;
    public int BubbleY { get; set; } = -1;
    public int BubbleWidth { get; set; }
    public int BubbleHeight { get; set; }
    public int TextX { get; set; } = -1;
    public int TextY { get; set; } = -1;
    public int TextWidth { get; set; }
    public int TextHeight { get; set; }
    public int GifX { get; set; } = -1;
    public int GifY { get; set; } = -1;
    public int GifWidth { get; set; }
    public int GifHeight { get; set; }
    public bool ShowResizeHandles { get; set; }
    public string AlarmGifPath { get; set; } = string.Empty;
    public string AlarmSoundPath { get; set; } = string.Empty;
    public string NormalSoundPath { get; set; } = string.Empty;
    public string SpeechBubblePath { get; set; } = string.Empty;
    public DateTime? AlarmDate { get; set; }
    public bool OneTimeAlarmCompleted { get; set; }

    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenAlarm");

    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");

    public static AlarmSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AlarmSettings>(File.ReadAllText(FilePath)) ?? new()
                : new();
        }
        catch
        {
            return new();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
}
