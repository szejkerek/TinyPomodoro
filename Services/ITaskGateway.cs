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

        /// <summary>
        /// True when the backend moves a task through statuses as you work it (ClickUp):
        /// activating it sets "in progress", deactivating returns it to "to do", and completing
        /// sends it to "review" instead of closing. False backends ignore the activate/deactivate
        /// calls and treat completion as a plain close.
        /// </summary>
        bool SupportsStatusWorkflow { get; }

        /// <summary>
        /// Point the gateway at its backend from the live settings. Each adapter reads only what it
        /// needs (Todoist: token + filter; ClickUp: token + list), so callers never reach past the
        /// seam to configure a specific backend.
        /// </summary>
        void Configure(AppSettings settings);

        Task<IReadOnlyList<TodoistProject>> GetProjectsAsync();
        Task<IReadOnlyList<TodoistTask>> GetActiveTasksAsync(string filter, string projectId);

        /// <summary>Mark a task as the one being worked on. Returns the status label now shown, or "".</summary>
        Task<string> ActivateTaskAsync(string taskId);

        /// <summary>Return a task to the not-started column. Returns the status label now shown, or "".</summary>
        Task<string> DeactivateTaskAsync(string taskId);

        Task CloseTaskAsync(string taskId);
    }
}
