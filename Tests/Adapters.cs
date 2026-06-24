using Pomodoro.Models;
using Pomodoro.Services;

namespace Pomodoro.Tests
{
    /// <summary>Test <see cref="IClock"/>: ticks happen only when the test asks. No real time.</summary>
    public sealed class ManualClock : IClock
    {
        public event Action? Tick;

        public bool IsRunning { get; private set; }

        /// <summary>Wall-clock time the session stamps finished pomodoros with. Fixed unless a test sets it.</summary>
        public DateTime Now { get; set; } = new DateTime(2026, 6, 23, 12, 0, 0);

        public void Start()
        {
            IsRunning = true;
        }

        public void Stop()
        {
            IsRunning = false;
        }

        /// <summary>Fire up to <paramref name="seconds"/> ticks, stopping early if the clock is stopped.</summary>
        public void Advance(int seconds)
        {
            for (int second = 0; second < seconds && IsRunning; second++)
            {
                Tick?.Invoke();
            }
        }
    }

    /// <summary>Test <see cref="ITaskGateway"/>: serves canned data, records closes.</summary>
    public sealed class InMemoryTodoistGateway : ITaskGateway
    {
        public List<TodoistProject> ProjectsToReturn { get; } = new List<TodoistProject>();

        // Key: project id when filtering by project, otherwise the filter string ("" = all).
        public Dictionary<string, List<TodoistTask>> TasksByKey { get; } = new Dictionary<string, List<TodoistTask>>();

        public List<string> ClosedTaskIds { get; } = new List<string>();

        private string token = string.Empty;

        public bool HasToken => token.Length > 0;

        public bool SupportsProjects { get; set; } = true;

        public void UseToken(string value)
        {
            token = value.Trim();
        }

        public Task<IReadOnlyList<TodoistProject>> GetProjectsAsync()
        {
            return Task.FromResult<IReadOnlyList<TodoistProject>>(ProjectsToReturn);
        }

        public Task<IReadOnlyList<TodoistTask>> GetActiveTasksAsync(string filter, string projectId)
        {
            string key = projectId.Length > 0 ? projectId : filter;
            List<TodoistTask> tasks = TasksByKey.TryGetValue(key, out List<TodoistTask>? found)
                ? found
                : new List<TodoistTask>();
            return Task.FromResult<IReadOnlyList<TodoistTask>>(tasks);
        }

        public Task CloseTaskAsync(string taskId)
        {
            ClosedTaskIds.Add(taskId);
            return Task.CompletedTask;
        }
    }

    /// <summary>Test <see cref="ISessionLog"/>: keeps completed pomodoros in memory, never touches disk.</summary>
    public sealed class InMemorySessionLog : ISessionLog
    {
        private readonly List<CompletedPomodoro> entries = new List<CompletedPomodoro>();

        public void Record(CompletedPomodoro entry)
        {
            entries.Add(entry);
        }

        public IReadOnlyList<CompletedPomodoro> All()
        {
            return entries;
        }
    }

    /// <summary>Test <see cref="ISettingsStore"/>: keeps settings in memory, never touches disk.</summary>
    public sealed class InMemorySettingsStore : ISettingsStore
    {
        private AppSettings settings;

        public InMemorySettingsStore(AppSettings seed)
        {
            settings = seed;
        }

        public int SaveCount { get; private set; }

        public AppSettings Load()
        {
            return settings;
        }

        public void Save(AppSettings value)
        {
            settings = value;
            SaveCount++;
        }
    }
}
