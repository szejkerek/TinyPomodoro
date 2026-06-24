using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
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

        private readonly HttpClient httpClient = new HttpClient();
        private string apiToken = string.Empty;

        public bool HasToken => apiToken.Length > 0;

        public bool SupportsProjects => true;

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
            using HttpResponseMessage response = await SendWithRetryAsync(() => BuildRequest(HttpMethod.Post, requestUrl));
            response.EnsureSuccessStatusCode();
        }

        private async Task<T> GetJsonAsync<T>(string requestUrl) where T : new()
        {
            using HttpResponseMessage response = await SendWithRetryAsync(() => BuildRequest(HttpMethod.Get, requestUrl));
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            T? parsed = JsonSerializer.Deserialize<T>(json);
            return parsed ?? new T();
        }

        private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> buildRequest)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    using HttpRequestMessage request = buildRequest();
                    HttpResponseMessage response = await httpClient.SendAsync(request);
                    if (attempt >= MaxAttempts || IsTransient(response.StatusCode) == false)
                    {
                        return response;
                    }

                    response.Dispose();
                }
                catch (HttpRequestException) when (attempt < MaxAttempts)
                {
                }
                catch (TaskCanceledException) when (attempt < MaxAttempts)
                {
                }

                await Task.Delay(TimeSpan.FromMilliseconds(RetryBackoffMs * attempt));
            }
        }

        private static bool IsTransient(HttpStatusCode status)
        {
            return status == HttpStatusCode.InternalServerError
                || status == HttpStatusCode.BadGateway
                || status == HttpStatusCode.ServiceUnavailable
                || status == HttpStatusCode.GatewayTimeout
                || status == HttpStatusCode.TooManyRequests;
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
