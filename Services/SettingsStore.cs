using System.IO;
using System.Text.Json;
using Pomodoro.Models;

namespace Pomodoro.Services
{
    public sealed class SettingsStore : ISettingsStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private readonly string settingsFilePath;

        public SettingsStore()
        {
            string appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string applicationDirectory = Path.Combine(appDataDirectory, "Pomodoro");
            Directory.CreateDirectory(applicationDirectory);
            settingsFilePath = Path.Combine(applicationDirectory, "settings.json");
        }

        public AppSettings Load()
        {
            if (!File.Exists(settingsFilePath))
            {
                return new AppSettings();
            }

            try
            {
                string json = File.ReadAllText(settingsFilePath);
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json);
                return loaded ?? new AppSettings();
            }
            catch (JsonException)
            {
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            string json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(settingsFilePath, json);
        }
    }
}
