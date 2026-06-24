using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>
    /// An <see cref="ITaskGateway"/> that forwards every call to whichever backend the user
    /// currently has selected (<see cref="AppSettings.ActiveSource"/>). This lets
    /// <see cref="TaskListModel"/> stay source-agnostic — it holds one gateway and never learns
    /// that more than one backend exists. The window flips the source by changing the setting.
    /// </summary>
    public sealed class TaskGatewayRouter : ITaskGateway
    {
        private readonly SettingsService settings;
        private readonly ITaskGateway todoist;
        private readonly ITaskGateway clickUp;

        public TaskGatewayRouter(SettingsService settings, ITaskGateway todoist, ITaskGateway clickUp)
        {
            this.settings = settings;
            this.todoist = todoist;
            this.clickUp = clickUp;
        }

        private ITaskGateway Active => settings.Current.ActiveSource == TaskSource.ClickUp ? clickUp : todoist;

        public bool HasToken => Active.HasToken;

        public bool SupportsProjects => Active.SupportsProjects;

        public void UseToken(string token) => Active.UseToken(token);

        public Task<IReadOnlyList<TodoistProject>> GetProjectsAsync() => Active.GetProjectsAsync();

        public Task<IReadOnlyList<TodoistTask>> GetActiveTasksAsync(string filter, string projectId) =>
            Active.GetActiveTasksAsync(filter, projectId);

        public Task CloseTaskAsync(string taskId) => Active.CloseTaskAsync(taskId);
    }
}
