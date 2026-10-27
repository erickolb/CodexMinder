using Microsoft.Win32;

namespace CodexMinder;

internal static class SystemTheme
{
    public static bool IsLight
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
        }
    }
}
