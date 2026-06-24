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
        private readonly ITaskGateway asana;

        public TaskGatewayRouter(SettingsService settings, ITaskGateway todoist, ITaskGateway clickUp, ITaskGateway asana)
        {
            this.settings = settings;
            this.todoist = todoist;
            this.clickUp = clickUp;
            this.asana = asana;
        }

        private ITaskGateway Active => settings.Current.ActiveSource switch
        {
            TaskSource.ClickUp => clickUp,
            TaskSource.Asana => asana,
            _ => todoist
        };

        public bool HasToken => Active.HasToken;

        public bool SupportsProjects => Active.SupportsProjects;

        public bool SupportsStatusWorkflow => Active.SupportsStatusWorkflow;

        // Configure every backend, not just the active one, so flipping the source later needs no re-wire.
        public void Configure(AppSettings appSettings)
        {
            todoist.Configure(appSettings);
            clickUp.Configure(appSettings);
            asana.Configure(appSettings);
        }

        public Task<IReadOnlyList<TodoistProject>> GetProjectsAsync() => Active.GetProjectsAsync();

        public Task<IReadOnlyList<TodoistTask>> GetActiveTasksAsync(string filter, string projectId) =>
            Active.GetActiveTasksAsync(filter, projectId);

        public Task<string> ActivateTaskAsync(string taskId) => Active.ActivateTaskAsync(taskId);

        public Task<string> DeactivateTaskAsync(string taskId) => Active.DeactivateTaskAsync(taskId);

        public Task CloseTaskAsync(string taskId) => Active.CloseTaskAsync(taskId);
    }
}
