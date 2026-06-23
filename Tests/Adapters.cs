using Pomodoro.Models;
using Pomodoro.Services;

namespace Pomodoro.Tests
{
    /// <summary>Test <see cref="IClock"/>: ticks happen only when the test asks. No real time.</summary>
    public sealed class ManualClock : IClock
    {
        public event Action? Tick;

        public bool IsRunning { get; private set; }

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

    /// <summary>Test <see cref="ITodoistGateway"/>: serves canned data, records closes.</summary>
    public sealed class InMemoryTodoistGateway : ITodoistGateway
    {
        public List<TodoistProject> ProjectsToReturn { get; } = new List<TodoistProject>();

        // Key: project id when filtering by project, otherwise the filter string ("" = all).
        public Dictionary<string, List<TodoistTask>> TasksByKey { get; } = new Dictionary<string, List<TodoistTask>>();

        public List<string> ClosedTaskIds { get; } = new List<string>();

        private string token = string.Empty;

        public bool HasToken => token.Length > 0;

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
