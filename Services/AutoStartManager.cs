using System.Diagnostics;
using Microsoft.Win32;

namespace Pomodoro.Services
{
    /// <summary>Toggles "launch at Windows login" via the per-user Run registry key.</summary>
    public sealed class AutoStartManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Pomodoro";

        public bool IsEnabled()
        {
            using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            if (runKey is null)
            {
                return false;
            }

            object? storedValue = runKey.GetValue(ValueName);
            return storedValue is not null;
        }

        public void Apply(bool shouldEnable)
        {
            using RegistryKey runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (shouldEnable)
            {
                runKey.SetValue(ValueName, $"\"{GetExecutablePath()}\"");
                return;
            }

            if (runKey.GetValue(ValueName) is not null)
            {
                runKey.DeleteValue(ValueName);
            }
        }

        private static string GetExecutablePath()
        {
            return Process.GetCurrentProcess().MainModule?.FileName ?? Environment.ProcessPath ?? string.Empty;
        }
    }
}
