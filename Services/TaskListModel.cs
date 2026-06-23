using System.Collections.ObjectModel;
using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>
    /// Owns the task list flow that used to be smeared across four window handlers:
    /// loading projects and tasks, the project-beats-filter precedence, selection persistence,
    /// closing a task, and the hint shown on empty/error. The window only binds to its state.
    /// </summary>
    public sealed class TaskListModel
    {
        public const string TokenMissingHint = "Dodaj token Todoist w ustawieniach (⚙), aby zobaczyć zadania.";
        private const string AllProjectsName = "Wszystkie";

        private readonly ITodoistGateway gateway;
        private readonly SettingsService settings;

        public TaskListModel(ITodoistGateway gateway, SettingsService settings)
        {
            this.gateway = gateway;
            this.settings = settings;
            Hint = TokenMissingHint;
        }

        public ObservableCollection<TodoistProject> Projects { get; } = new ObservableCollection<TodoistProject>();
        public ObservableCollection<TodoistTask> Tasks { get; } = new ObservableCollection<TodoistTask>();

        public string Hint { get; private set; }
        public event Action? HintChanged;

        public bool HasToken => gateway.HasToken;
        public string SelectedProjectId => settings.Current.SelectedProjectId;

        public async Task SyncAsync()
        {
            if (!gateway.HasToken)
            {
                SetHint(TokenMissingHint);
                return;
            }

            await LoadProjectsAsync();
            await LoadTasksAsync();
        }

        public async Task SelectProjectAsync(string projectId)
        {
            settings.Update(current => current.SelectedProjectId = projectId);
            await LoadTasksAsync();
        }

        public async Task CloseTaskAsync(string taskId)
        {
            try
            {
                await gateway.CloseTaskAsync(taskId);
                TodoistTask? closed = Tasks.FirstOrDefault(task => task.Id == taskId);
                if (closed is not null)
                {
                    Tasks.Remove(closed);
                }
            }
            catch (Exception error)
            {
                SetHint($"Nie udało się odznaczyć: {error.Message}");
            }
        }

        private async Task LoadProjectsAsync()
        {
            try
            {
                IReadOnlyList<TodoistProject> fetched = await gateway.GetProjectsAsync();

                Projects.Clear();
                Projects.Add(new TodoistProject { Id = string.Empty, Name = AllProjectsName });
                foreach (TodoistProject project in fetched)
                {
                    Projects.Add(project);
                }

                bool selectionStillExists = Projects.Any(project => project.Id == settings.Current.SelectedProjectId);
                if (!selectionStillExists)
                {
                    settings.Update(current => current.SelectedProjectId = string.Empty);
                }
            }
            catch (Exception error)
            {
                SetHint($"Błąd Todoist (projekty): {error.Message}");
            }
        }

        private async Task LoadTasksAsync()
        {
            try
            {
                IReadOnlyList<TodoistTask> activeTasks =
                    await gateway.GetActiveTasksAsync(settings.Current.TodoistFilter, settings.Current.SelectedProjectId);

                Tasks.Clear();
                foreach (TodoistTask task in activeTasks)
                {
                    Tasks.Add(task);
                }

                SetHint(Tasks.Count == 0 ? "Brak zadań. 🎉" : string.Empty);
            }
            catch (Exception error)
            {
                SetHint($"Błąd Todoist: {error.Message}");
            }
        }

        private void SetHint(string message)
        {
            Hint = message;
            HintChanged?.Invoke();
        }
    }
}
