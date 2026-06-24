using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>
    /// The seam over a task backend (Todoist, ClickUp, …). Production talks HTTP;
    /// tests use an in-memory adapter, so the task-list flow runs without a network round-trip.
    /// </summary>
    public interface ITaskGateway
    {
        bool HasToken { get; }

        /// <summary>
        /// True when the backend exposes a project list to choose from (Todoist).
        /// False for single-scope backends like a single configured ClickUp list, where
        /// there is no project picker.
        /// </summary>
        bool SupportsProjects { get; }

        void UseToken(string token);
        Task<IReadOnlyList<TodoistProject>> GetProjectsAsync();
        Task<IReadOnlyList<TodoistTask>> GetActiveTasksAsync(string filter, string projectId);
        Task CloseTaskAsync(string taskId);
    }
}
