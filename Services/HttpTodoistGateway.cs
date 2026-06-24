using System.Net.Http;
using System.Net.Http.Headers;
using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>Production <see cref="ITaskGateway"/>: the Todoist unified API v1 over HTTP.</summary>
    public sealed class HttpTodoistGateway : ITaskGateway
    {
        private const string ApiBase = "https://api.todoist.com/api/v1";
        private const int PageSize = 200;
        private const int MaxPages = 5;

        // Todoist's gateway occasionally returns a transient 5xx/429 for a second or two;
        // retry a few times with a linear backoff before surfacing the error to the user.
        private const int MaxAttempts = 4;
        private const int RetryBackoffMs = 400;

        private readonly HttpJsonTransport transport = new HttpJsonTransport(new HttpClient(), MaxAttempts, RetryBackoffMs);
        private string apiToken = string.Empty;

        public bool HasToken => apiToken.Length > 0;

        public bool SupportsProjects => true;

        public bool SupportsStatusWorkflow => false;

        public Task<string> ActivateTaskAsync(string taskId)
        {
            return Task.FromResult(string.Empty);
        }

        public Task<string> DeactivateTaskAsync(string taskId)
        {
            return Task.FromResult(string.Empty);
        }

        public void Configure(AppSettings settings)
        {
            apiToken = settings.TodoistToken.Trim();
        }

        public async Task<IReadOnlyList<TodoistProject>> GetProjectsAsync()
        {
            List<TodoistProject> collected = new List<TodoistProject>();
            string? cursor = null;
            int pageIndex = 0;

            do
            {
                string requestUrl = AppendCursor($"{ApiBase}/projects?limit={PageSize}", cursor);
                TodoistProjectPage page = await GetJsonAsync<TodoistProjectPage>(requestUrl);
                collected.AddRange(page.Results);
                cursor = page.NextCursor;
                pageIndex++;
            }
            while (HasMorePages(cursor, pageIndex));

            return collected;
        }

        public async Task<IReadOnlyList<TodoistTask>> GetActiveTasksAsync(string filter, string projectId)
        {
            List<TodoistTask> collected = new List<TodoistTask>();
            string? cursor = null;
            int pageIndex = 0;

            do
            {
                string requestUrl = BuildTaskListUrl(filter, projectId, cursor);
                TodoistTaskPage page = await GetJsonAsync<TodoistTaskPage>(requestUrl);
                collected.AddRange(page.Results);
                cursor = page.NextCursor;
                pageIndex++;
            }
            while (HasMorePages(cursor, pageIndex));

            foreach (TodoistTask task in collected)
            {
                task.DueDate = DueDateLabel.FromTodoist(task.Due?.Date);
            }

            await TagWithSectionNamesAsync(collected);

            // Match Todoist's manual ordering instead of raw API order.
            return collected.OrderBy(task => task.ChildOrder).ToList();
        }

        // Informational only: show which section each task sits in. One section lookup per task load.
        private async Task TagWithSectionNamesAsync(List<TodoistTask> tasks)
        {
            bool anySectioned = tasks.Any(task => string.IsNullOrEmpty(task.SectionId) == false);
            if (anySectioned == false)
            {
                return;
            }

            Dictionary<string, string> namesById = await GetSectionNamesAsync();
            foreach (TodoistTask task in tasks)
            {
                if (task.SectionId is not null && namesById.TryGetValue(task.SectionId, out string? name))
                {
                    task.SectionName = name;
                }
            }
        }

        private async Task<Dictionary<string, string>> GetSectionNamesAsync()
        {
            Dictionary<string, string> namesById = new Dictionary<string, string>();
            string? cursor = null;
            int pageIndex = 0;

            do
            {
                string requestUrl = AppendCursor($"{ApiBase}/sections?limit={PageSize}", cursor);
                TodoistSectionPage page = await GetJsonAsync<TodoistSectionPage>(requestUrl);
                foreach (TodoistSection section in page.Results)
                {
                    namesById[section.Id] = section.Name;
                }

                cursor = page.NextCursor;
                pageIndex++;
            }
            while (HasMorePages(cursor, pageIndex));

            return namesById;
        }

        public async Task CloseTaskAsync(string taskId)
        {
            string requestUrl = $"{ApiBase}/tasks/{taskId}/close";
            using HttpResponseMessage response =
                await transport.SendWithRetryAsync(() => BuildRequest(HttpMethod.Post, requestUrl));
            response.EnsureSuccessStatusCode();
        }

        private Task<T> GetJsonAsync<T>(string requestUrl) where T : new()
        {
            return transport.GetJsonAsync<T>(() => BuildRequest(HttpMethod.Get, requestUrl));
        }

        private static bool HasMorePages(string? cursor, int pageIndex)
        {
            return cursor is not null && cursor.Length > 0 && pageIndex < MaxPages;
        }

        private static string BuildTaskListUrl(string filter, string projectId, string? cursor)
        {
            string trimmedProject = projectId.Trim();
            if (trimmedProject.Length > 0)
            {
                return AppendCursor($"{ApiBase}/tasks?project_id={trimmedProject}&limit={PageSize}", cursor);
            }

            string trimmedFilter = filter.Trim();
            if (trimmedFilter.Length > 0)
            {
                string encoded = Uri.EscapeDataString(trimmedFilter);
                return AppendCursor($"{ApiBase}/tasks/filter?query={encoded}&limit={PageSize}", cursor);
            }

            return AppendCursor($"{ApiBase}/tasks?limit={PageSize}", cursor);
        }

        private static string AppendCursor(string url, string? cursor)
        {
            if (cursor is null || cursor.Length == 0)
            {
                return url;
            }

            return $"{url}&cursor={Uri.EscapeDataString(cursor)}";
        }

        private HttpRequestMessage BuildRequest(HttpMethod method, string url)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            return request;
        }
    }
}
