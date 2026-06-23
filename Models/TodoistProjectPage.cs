using System.Text.Json.Serialization;

namespace Pomodoro.Models
{
    /// <summary>One page of the Todoist API v1 paginated project response.</summary>
    public sealed class TodoistProjectPage
    {
        [JsonPropertyName("results")]
        public List<TodoistProject> Results { get; set; } = new List<TodoistProject>();

        [JsonPropertyName("next_cursor")]
        public string? NextCursor { get; set; }
    }
}
