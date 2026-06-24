using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>
    /// A task source with no backend — used for Asana, where selecting it only declares the
    /// "at work" context. It never has a token, so the task list stays empty and the model
    /// shows its no-integration hint instead of tasks.
    /// </summary>
    public sealed class NullTaskGateway : ITaskGateway
    {
        public bool HasToken => false;

        public bool SupportsProjects => false;

        public bool SupportsStatusWorkflow => false;

        public void Configure(AppSettings settings)
        {
        }

        public Task<string> ActivateTaskAsync(string taskId)
        {
            return Task.FromResult(string.Empty);
        }

        public Task<string> DeactivateTaskAsync(string taskId)
        {
            return Task.FromResult(string.Empty);
        }

        public Task<IReadOnlyList<TodoistProject>> GetProjectsAsync()
        {
            return Task.FromResult<IReadOnlyList<TodoistProject>>(Array.Empty<TodoistProject>());
        }

        public Task<IReadOnlyList<TodoistTask>> GetActiveTasksAsync(string filter, string projectId)
        {
            return Task.FromResult<IReadOnlyList<TodoistTask>>(Array.Empty<TodoistTask>());
        }

        public Task CloseTaskAsync(string taskId)
        {
            return Task.CompletedTask;
        }
    }
}
