using Microsoft.Win32;

namespace ScreenAlarm;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScreenAlarm";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var registeredCommand = key?.GetValue(ValueName) as string;
            return string.Equals(registeredCommand, GetStartupCommand(),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Windows 시작 프로그램 설정을 열 수 없습니다.");

        if (enabled)
            key.SetValue(ValueName, GetStartupCommand(), RegistryValueKind.String);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string GetStartupCommand() => $"\"{Application.ExecutablePath}\"";
}
