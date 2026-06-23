using Pomodoro.Models;
using Pomodoro.Services;
using Xunit;

namespace Pomodoro.Tests
{
    public class TaskListModelTests
    {
        private static SettingsService SettingsWith(AppSettings seed)
        {
            return new SettingsService(new InMemorySettingsStore(seed));
        }

        [Fact]
        public async Task Without_a_token_it_shows_the_token_hint()
        {
            InMemoryTodoistGateway gateway = new InMemoryTodoistGateway();
            TaskListModel model = new TaskListModel(gateway, SettingsWith(new AppSettings()));

            await model.SyncAsync();

            Assert.Equal(TaskListModel.TokenMissingHint, model.Hint);
            Assert.Empty(model.Tasks);
        }

        [Fact]
        public async Task Sync_prepends_all_projects_and_loads_tasks()
        {
            InMemoryTodoistGateway gateway = new InMemoryTodoistGateway();
            gateway.UseToken("t");
            gateway.ProjectsToReturn.Add(new TodoistProject { Id = "P1", Name = "Portfolio" });
            gateway.TasksByKey[""] = new List<TodoistTask> { new TodoistTask { Id = "1", Content = "a" } };

            TaskListModel model = new TaskListModel(gateway, SettingsWith(new AppSettings()));

            await model.SyncAsync();

            Assert.Equal(2, model.Projects.Count);
            Assert.Equal("Wszystkie", model.Projects[0].Name);
            Assert.Single(model.Tasks);
        }

        [Fact]
        public async Task Selected_project_beats_the_filter()
        {
            InMemoryTodoistGateway gateway = new InMemoryTodoistGateway();
            gateway.UseToken("t");
            gateway.ProjectsToReturn.Add(new TodoistProject { Id = "P1", Name = "Portfolio" });
            gateway.TasksByKey["P1"] = new List<TodoistTask> { new TodoistTask { Id = "byProject", Content = "p" } };
            gateway.TasksByKey["today"] = new List<TodoistTask> { new TodoistTask { Id = "byFilter", Content = "f" } };

            AppSettings settings = new AppSettings { SelectedProjectId = "P1", TodoistFilter = "today" };
            TaskListModel model = new TaskListModel(gateway, SettingsWith(settings));

            await model.SyncAsync();

            Assert.Single(model.Tasks);
            Assert.Equal("byProject", model.Tasks[0].Id);
        }

        [Fact]
        public async Task Missing_stored_project_falls_back_to_all()
        {
            InMemoryTodoistGateway gateway = new InMemoryTodoistGateway();
            gateway.UseToken("t");
            gateway.ProjectsToReturn.Add(new TodoistProject { Id = "P1", Name = "Portfolio" });
            gateway.TasksByKey[""] = new List<TodoistTask>();

            AppSettings settings = new AppSettings { SelectedProjectId = "ghost" };
            TaskListModel model = new TaskListModel(gateway, SettingsWith(settings));

            await model.SyncAsync();

            Assert.Equal(string.Empty, model.SelectedProjectId);
        }

        [Fact]
        public async Task Closing_a_task_removes_it_and_calls_the_gateway()
        {
            InMemoryTodoistGateway gateway = new InMemoryTodoistGateway();
            gateway.UseToken("t");
            gateway.ProjectsToReturn.Add(new TodoistProject { Id = "P1", Name = "Portfolio" });
            gateway.TasksByKey[""] = new List<TodoistTask>
            {
                new TodoistTask { Id = "keep", Content = "k" },
                new TodoistTask { Id = "done", Content = "d" }
            };

            TaskListModel model = new TaskListModel(gateway, SettingsWith(new AppSettings()));
            await model.SyncAsync();

            await model.CloseTaskAsync("done");

            Assert.Single(model.Tasks);
            Assert.Equal("keep", model.Tasks[0].Id);
            Assert.Contains("done", gateway.ClosedTaskIds);
        }
    }
}
