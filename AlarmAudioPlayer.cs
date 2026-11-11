using System.Runtime.InteropServices;

namespace ScreenAlarm;

internal sealed class AlarmAudioPlayer : IDisposable
{
    private readonly string alias = $"ScreenAlarmAudio{Guid.NewGuid():N}";
    private bool opened;

    public bool PlayLooping(string path) => Play(path, repeat: true);

    public bool PlayOnce(string path) => Play(path, repeat: false);

    private bool Play(string path, bool repeat)
    {
        Stop();
        if (!File.Exists(path))
            return false;

        var escapedPath = path.Replace("\"", "\"\"");
        if (MciSendString($"open \"{escapedPath}\" alias {alias}", null, 0, IntPtr.Zero) != 0)
            return false;

        opened = true;
        var repeatCommand = repeat ? " repeat" : string.Empty;
        if (MciSendString($"play {alias}{repeatCommand}", null, 0, IntPtr.Zero) == 0)
            return true;

        Stop();
        return false;
    }

    public void Stop()
    {
        if (!opened)
            return;

        MciSendString($"stop {alias}", null, 0, IntPtr.Zero);
        MciSendString($"close {alias}", null, 0, IntPtr.Zero);
        opened = false;
    }

    public void Dispose() => Stop();

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
    private static extern int MciSendString(string command, string? returnValue,
        int returnLength, IntPtr callback);
}
