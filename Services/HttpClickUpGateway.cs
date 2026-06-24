using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pomodoro.Models;

namespace Pomodoro.Services
{
    /// <summary>
    /// Production <see cref="ITaskGateway"/> for ClickUp (API v2), scoped to one configured List.
    /// ClickUp nests tasks under Workspace → Space → Folder → List, so there is no flat project
    /// list to expose: the widget points at a single List id and shows its open tasks.
    /// Auth is the raw personal token in the <c>Authorization</c> header (no <c>Bearer</c> prefix).
    /// </summary>
    public sealed class HttpClickUpGateway : ITaskGateway
    {
        private const string ApiBase = "https://api.clickup.com/api/v2";
        private const int MaxPages = 5;

        // ClickUp occasionally returns a transient 5xx/429; retry a few times with linear backoff.
        private const int MaxAttempts = 4;
        private const int RetryBackoffMs = 400;

        private const string ClosedStatusType = "closed";
        private const string DoneStatusType = "done";
        private const string FallbackClosedStatus = "complete";

        private readonly HttpClient httpClient = new HttpClient();
        private string apiToken = string.Empty;
        private string listId = string.Empty;

        // The status name a task must take to count as done is list-specific; resolved once and cached.
        private string? closedStatusName;

        // Tasks are scoped to the token owner, so the list only shows what's assigned to me.
        // Resolved from the token once and cached.
        private long? currentUserId;

        public bool HasToken => apiToken.Length > 0 && listId.Length > 0;

        public bool SupportsProjects => false;

        public void UseToken(string token)
        {
            string trimmed = token.Trim();
            if (trimmed != apiToken)
            {
                currentUserId = null;
            }

            apiToken = trimmed;
        }

        public void UseList(string listIdValue)
        {
            string trimmed = listIdValue.Trim();
            if (trimmed != listId)
            {
                closedStatusName = null;
            }

            listId = trimmed;
        }

        public Task<IReadOnlyList<TodoistProject>> GetProjectsAsync()
        {
            return Task.FromResult<IReadOnlyList<TodoistProject>>(Array.Empty<TodoistProject>());
        }

        public async Task<IReadOnlyList<TodoistTask>> GetActiveTasksAsync(string filter, string projectId)
        {
            long userId = await ResolveCurrentUserIdAsync();
            List<TodoistTask> collected = new List<TodoistTask>();

            for (int page = 0; page < MaxPages; page++)
            {
                string requestUrl = $"{ApiBase}/list/{listId}/task?include_closed=false&assignees%5B%5D={userId}&page={page}";
                ClickUpTaskPage fetched = await GetJsonAsync<ClickUpTaskPage>(requestUrl);

                foreach (ClickUpTask task in fetched.Tasks)
                {
                    collected.Add(new TodoistTask { Id = task.Id, Content = task.Name });
                }

                if (fetched.IsLastPage || fetched.Tasks.Count == 0)
                {
                    break;
                }
            }

            return collected;
        }

        public async Task CloseTaskAsync(string taskId)
        {
            string status = await ResolveClosedStatusNameAsync();
            string requestUrl = $"{ApiBase}/task/{taskId}";
            string body = JsonSerializer.Serialize(new ClickUpStatusUpdate { Status = status });

            using HttpResponseMessage response =
                await SendWithRetryAsync(() => BuildJsonRequest(HttpMethod.Put, requestUrl, body));
            response.EnsureSuccessStatusCode();
        }

        private async Task<long> ResolveCurrentUserIdAsync()
        {
            if (currentUserId is not null)
            {
                return currentUserId.Value;
            }

            ClickUpUserResponse response = await GetJsonAsync<ClickUpUserResponse>($"{ApiBase}/user");
            currentUserId = response.User.Id;
            return currentUserId.Value;
        }

        private async Task<string> ResolveClosedStatusNameAsync()
        {
            if (closedStatusName is not null)
            {
                return closedStatusName;
            }

            ClickUpList list = await GetJsonAsync<ClickUpList>($"{ApiBase}/list/{listId}");
            ClickUpStatus? closed = list.Statuses.FirstOrDefault(status => status.Type == ClosedStatusType)
                ?? list.Statuses.FirstOrDefault(status => status.Type == DoneStatusType);

            closedStatusName = closed?.Status ?? FallbackClosedStatus;
            return closedStatusName;
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

        private HttpRequestMessage BuildRequest(HttpMethod method, string url)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, url);
            request.Headers.TryAddWithoutValidation("Authorization", apiToken);
            return request;
        }

        private HttpRequestMessage BuildJsonRequest(HttpMethod method, string url, string body)
        {
            HttpRequestMessage request = BuildRequest(method, url);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return request;
        }
    }

    // --- ClickUp wire DTOs (only this gateway deserializes them) ---

    internal sealed class ClickUpTaskPage
    {
        [JsonPropertyName("tasks")]
        public List<ClickUpTask> Tasks { get; set; } = new List<ClickUpTask>();

        [JsonPropertyName("last_page")]
        public bool IsLastPage { get; set; }
    }

    internal sealed class ClickUpTask
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    internal sealed class ClickUpList
    {
        [JsonPropertyName("statuses")]
        public List<ClickUpStatus> Statuses { get; set; } = new List<ClickUpStatus>();
    }

    internal sealed class ClickUpStatus
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }

    internal sealed class ClickUpStatusUpdate
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    internal sealed class ClickUpUserResponse
    {
        [JsonPropertyName("user")]
        public ClickUpUser User { get; set; } = new ClickUpUser();
    }

    internal sealed class ClickUpUser
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
    }
}
