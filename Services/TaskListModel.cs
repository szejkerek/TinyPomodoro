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
        public const string TokenMissingHint = "Add a Todoist token in settings (⚙) to see your tasks.";
        public const string ClickUpTokenMissingHint = "Add a ClickUp token and List ID in settings (⚙) to see your tasks.";
        private const string AllProjectsName = "All";

        private readonly ITaskGateway gateway;
        private readonly SettingsService settings;

        public TaskListModel(ITaskGateway gateway, SettingsService settings)
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
        public bool SupportsProjects => gateway.SupportsProjects;
        public TaskSource ActiveSource => settings.Current.ActiveSource;
        public string SelectedProjectId => settings.Current.SelectedProjectId;
        public string? FocusedTaskId { get; private set; }

        public async Task SyncAsync()
        {
            FocusedTaskId = null;
            if (gateway.HasToken == false)
            {
                Projects.Clear();
                Tasks.Clear();
                SetHint(TokenHint());
                return;
            }

            if (gateway.SupportsProjects)
            {
                await LoadProjectsAsync();
            }
            else
            {
                Projects.Clear();
            }

            await LoadTasksAsync();
        }

        public async Task SwitchSourceAsync(TaskSource source)
        {
            settings.Update(current => current.ActiveSource = source);
            await SyncAsync();
        }

        public async Task SelectProjectAsync(string projectId)
        {
            settings.Update(current => current.SelectedProjectId = projectId);
            await LoadTasksAsync();
        }

        /// <summary>
        /// Mark a task as the one being worked on: pin it to the top of the list and highlight it,
        /// clearing the highlight from whatever was focused before.
        /// </summary>
        public void Focus(string taskId)
        {
            TodoistTask? target = Tasks.FirstOrDefault(task => task.Id == taskId);
            if (target is null)
            {
                return;
            }

            foreach (TodoistTask task in Tasks)
            {
                task.IsFocused = task == target;
            }

            FocusedTaskId = taskId;

            int index = Tasks.IndexOf(target);
            if (index > 0)
            {
                Tasks.Move(index, 0);
            }
        }

        /// <summary>
        /// Focus a task and, on backends that track it, move it to "in progress" while returning the
        /// previously-focused task to "to do".
        /// </summary>
        public async Task FocusAsync(string taskId)
        {
            string? previous = FocusedTaskId;
            Focus(taskId);

            if (gateway.SupportsStatusWorkflow == false)
            {
                return;
            }

            try
            {
                if (previous is not null && previous != taskId)
                {
                    ApplyStatus(previous, await gateway.DeactivateTaskAsync(previous));
                }

                ApplyStatus(taskId, await gateway.ActivateTaskAsync(taskId));
            }
            catch (Exception error)
            {
                SetHint($"ClickUp error: {error.Message}");
            }
        }

        private void ApplyStatus(string taskId, string status)
        {
            if (status.Length == 0)
            {
                return;
            }

            TodoistTask? task = Tasks.FirstOrDefault(candidate => candidate.Id == taskId);
            if (task is not null)
            {
                task.Status = status;
            }
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
                SetHint($"Could not complete task: {error.Message}");
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
                if (selectionStillExists == false)
                {
                    settings.Update(current => current.SelectedProjectId = string.Empty);
                }
            }
            catch (Exception error)
            {
                SetHint($"Todoist error (projects): {error.Message}");
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

                SetHint(Tasks.Count == 0 ? "No tasks." : string.Empty);
            }
            catch (Exception error)
            {
                SetHint($"Todoist error: {error.Message}");
            }
        }

        private string TokenHint()
        {
            return settings.Current.ActiveSource switch
            {
                TaskSource.ClickUp => ClickUpTokenMissingHint,
                TaskSource.Asana => string.Empty,
                _ => TokenMissingHint
            };
        }

        private void SetHint(string message)
        {
            Hint = message;
            HintChanged?.Invoke();
        }
    }
}
