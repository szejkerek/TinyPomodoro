using System.IO;
using System.Text.Json;
using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>
    /// Append-only <see cref="ISessionLog"/> backed by a JSON-lines file (one <see cref="CompletedPomodoro"/>
    /// per line). Append-only means a crash mid-write can lose at most the last line, never the history.
    /// </summary>
    public sealed class JsonSessionLog : ISessionLog
    {
        private const string LogFileName = "sessions.jsonl";

        private readonly string logFilePath;
        private readonly List<CompletedPomodoro> entries;

        public JsonSessionLog()
            : this(DefaultLogFilePath())
        {
        }

        public JsonSessionLog(string logFilePath)
        {
            this.logFilePath = logFilePath;
            entries = ReadAll(logFilePath);
        }

        public void Record(CompletedPomodoro entry)
        {
            entries.Add(entry);
            string line = JsonSerializer.Serialize(entry);
            File.AppendAllText(logFilePath, line + Environment.NewLine);
        }

        public IReadOnlyList<CompletedPomodoro> All()
        {
            return entries;
        }

        private static string DefaultLogFilePath()
        {
            string appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string applicationDirectory = Path.Combine(appDataDirectory, "Pomodoro");
            Directory.CreateDirectory(applicationDirectory);
            return Path.Combine(applicationDirectory, LogFileName);
        }

        private static List<CompletedPomodoro> ReadAll(string path)
        {
            List<CompletedPomodoro> loaded = new List<CompletedPomodoro>();
            if (File.Exists(path) == false)
            {
                return loaded;
            }

            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Length == 0)
                {
                    continue;
                }

                CompletedPomodoro? entry = TryDeserialize(line);
                if (entry != null)
                {
                    loaded.Add(entry);
                }
            }

            return loaded;
        }

        private static CompletedPomodoro? TryDeserialize(string line)
        {
            try
            {
                return JsonSerializer.Deserialize<CompletedPomodoro>(line);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
