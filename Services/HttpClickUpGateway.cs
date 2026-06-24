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

        // Status types ClickUp tags each status with, and the name fragments used to pick the
        // three workflow columns we drive. A list's status names are custom, so we match by
        // intent (name fragment first, then type) and fall back to a sensible literal.
        private const string ClosedStatusType = "closed";
        private const string DoneStatusType = "done";
        private const string CustomStatusType = "custom";
        private const string UnstartedStatusType = "unstarted";
        private const string ReviewNameFragment = "review";
        private const string InProgressNameFragment = "progress";
        private const string ToDoNameFragment = "to do";
        private const string ToDoNameFragmentAlt = "todo";
        private const string FallbackToDoStatus = "to do";
        private const string FallbackInProgressStatus = "in progress";
        private const string FallbackReviewStatus = "review";

        private readonly HttpJsonTransport transport = new HttpJsonTransport(new HttpClient(), MaxAttempts, RetryBackoffMs);
        private string apiToken = string.Empty;
        private string listId = string.Empty;

        // The list's workflow column names are custom; resolved from the list once and cached.
        private WorkflowStatuses? workflow;

        // Tasks are scoped to the token owner, so the list only shows what's assigned to me.
        // Resolved from the token once and cached.
        private long? currentUserId;

        public bool HasToken => apiToken.Length > 0 && listId.Length > 0;

        public bool SupportsProjects => false;

        public bool SupportsStatusWorkflow => true;

        public void Configure(AppSettings settings)
        {
            string newToken = settings.ClickUpToken.Trim();
            if (newToken != apiToken)
            {
                currentUserId = null;
            }

            apiToken = newToken;

            string newList = settings.ClickUpListId.Trim();
            if (newList != listId)
            {
                workflow = null;
            }

            listId = newList;
        }

        public Task<IReadOnlyList<TodoistProject>> GetProjectsAsync()
        {
            return Task.FromResult<IReadOnlyList<TodoistProject>>(Array.Empty<TodoistProject>());
        }

        public async Task<IReadOnlyList<TodoistTask>> GetActiveTasksAsync(string filter, string projectId)
        {
            WorkflowStatuses statuses = await ResolveWorkflowAsync();
            long userId = await ResolveCurrentUserIdAsync();
            List<TodoistTask> collected = new List<TodoistTask>();

            for (int page = 0; page < MaxPages; page++)
            {
                string requestUrl = $"{ApiBase}/list/{listId}/task?include_closed=false&assignees%5B%5D={userId}&page={page}";
                ClickUpTaskPage fetched = await GetJsonAsync<ClickUpTaskPage>(requestUrl);

                foreach (ClickUpTask task in fetched.Tasks)
                {
                    string statusName = task.Status?.Status ?? string.Empty;
                    string statusType = task.Status?.Type ?? string.Empty;
                    if (IsHidden(statusName, statusType, statuses))
                    {
                        continue;
                    }

                    collected.Add(new TodoistTask
                    {
                        Id = task.Id,
                        Content = task.Name,
                        Status = statusName,
                        DueDate = DueDateLabel.FromClickUpMillis(task.DueDate)
                    });
                }

                if (fetched.IsLastPage || fetched.Tasks.Count == 0)
                {
                    break;
                }
            }

            return collected;
        }

        public async Task<string> ActivateTaskAsync(string taskId)
        {
            WorkflowStatuses statuses = await ResolveWorkflowAsync();
            await PutStatusAsync(taskId, statuses.InProgress);
            return statuses.InProgress;
        }

        public async Task<string> DeactivateTaskAsync(string taskId)
        {
            WorkflowStatuses statuses = await ResolveWorkflowAsync();
            await PutStatusAsync(taskId, statuses.ToDo);
            return statuses.ToDo;
        }

        // "Completing" a task here means sending it to review, not closing it — review tasks
        // are hidden on the next load, same as done ones.
        public async Task CloseTaskAsync(string taskId)
        {
            WorkflowStatuses statuses = await ResolveWorkflowAsync();
            await PutStatusAsync(taskId, statuses.Review);
        }

        private async Task PutStatusAsync(string taskId, string status)
        {
            string requestUrl = $"{ApiBase}/task/{taskId}";
            string body = JsonSerializer.Serialize(new ClickUpStatusUpdate { Status = status });

            using HttpResponseMessage response =
                await transport.SendWithRetryAsync(() => BuildJsonRequest(HttpMethod.Put, requestUrl, body));
            response.EnsureSuccessStatusCode();
        }

        private static bool IsHidden(string statusName, string statusType, WorkflowStatuses statuses)
        {
            if (statusType == ClosedStatusType || statusType == DoneStatusType)
            {
                return true;
            }

            return string.Equals(statusName, statuses.Review, StringComparison.OrdinalIgnoreCase);
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

        private async Task<WorkflowStatuses> ResolveWorkflowAsync()
        {
            if (workflow is not null)
            {
                return workflow;
            }

            ClickUpList list = await GetJsonAsync<ClickUpList>($"{ApiBase}/list/{listId}");
            List<ClickUpStatus> statuses = list.Statuses;

            string toDo = NameContaining(statuses, ToDoNameFragment)
                ?? NameContaining(statuses, ToDoNameFragmentAlt)
                ?? OfType(statuses, UnstartedStatusType)
                ?? statuses.FirstOrDefault()?.Status
                ?? FallbackToDoStatus;

            string inProgress = NameContaining(statuses, InProgressNameFragment)
                ?? OfType(statuses, CustomStatusType)
                ?? FallbackInProgressStatus;

            string review = NameContaining(statuses, ReviewNameFragment)
                ?? OfType(statuses, ClosedStatusType)
                ?? OfType(statuses, DoneStatusType)
                ?? FallbackReviewStatus;

            workflow = new WorkflowStatuses(toDo, inProgress, review);
            return workflow;
        }

        private static string? NameContaining(List<ClickUpStatus> statuses, string fragment)
        {
            return statuses
                .FirstOrDefault(status => status.Status.Contains(fragment, StringComparison.OrdinalIgnoreCase))?.Status;
        }

        private static string? OfType(List<ClickUpStatus> statuses, string type)
        {
            return statuses.FirstOrDefault(status => status.Type == type)?.Status;
        }

        private Task<T> GetJsonAsync<T>(string requestUrl) where T : new()
        {
            return transport.GetJsonAsync<T>(() => BuildRequest(HttpMethod.Get, requestUrl));
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

        [JsonPropertyName("status")]
        public ClickUpStatus? Status { get; set; }

        [JsonPropertyName("due_date")]
        public string? DueDate { get; set; }
    }

    /// <summary>The three list columns the widget drives, resolved to this list's custom names.</summary>
    internal sealed record WorkflowStatuses(string ToDo, string InProgress, string Review);

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
