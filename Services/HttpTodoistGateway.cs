using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>Production <see cref="ITodoistGateway"/>: the Todoist unified API v1 over HTTP.</summary>
    public sealed class HttpTodoistGateway : ITodoistGateway
    {
        private const string ApiBase = "https://api.todoist.com/api/v1";
        private const int PageSize = 200;
        private const int MaxPages = 5;

        private readonly HttpClient httpClient = new HttpClient();
        private string apiToken = string.Empty;

        public bool HasToken => apiToken.Length > 0;

        public void UseToken(string token)
        {
            apiToken = token.Trim();
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

            // Match Todoist's manual ordering instead of raw API order.
            return collected.OrderBy(task => task.ChildOrder).ToList();
        }

        public async Task CloseTaskAsync(string taskId)
        {
            string requestUrl = $"{ApiBase}/tasks/{taskId}/close";
            using HttpRequestMessage request = BuildRequest(HttpMethod.Post, requestUrl);
            using HttpResponseMessage response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        private async Task<T> GetJsonAsync<T>(string requestUrl) where T : new()
        {
            using HttpRequestMessage request = BuildRequest(HttpMethod.Get, requestUrl);
            using HttpResponseMessage response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            T? parsed = JsonSerializer.Deserialize<T>(json);
            return parsed ?? new T();
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
