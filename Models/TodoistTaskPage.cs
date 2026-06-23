using System.Text.Json.Serialization;

namespace Pomodoro.Models
{
    /// <summary>One page of the Todoist API v1 paginated task response.</summary>
    public sealed class TodoistTaskPage
    {
        [JsonPropertyName("results")]
        public List<TodoistTask> Results { get; set; } = new List<TodoistTask>();

        [JsonPropertyName("next_cursor")]
        public string? NextCursor { get; set; }
    }
}
