using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>
    /// The seam over Todoist. Production talks HTTP (<see cref="HttpTodoistGateway"/>);
    /// tests use an in-memory adapter, so the task-list flow runs without a network round-trip.
    /// </summary>
    public interface ITodoistGateway
    {
        bool HasToken { get; }
        void UseToken(string token);
        Task<IReadOnlyList<TodoistProject>> GetProjectsAsync();
        Task<IReadOnlyList<TodoistTask>> GetActiveTasksAsync(string filter, string projectId);
        Task CloseTaskAsync(string taskId);
    }
}
