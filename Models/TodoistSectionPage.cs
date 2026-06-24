using System.Text.Json.Serialization;

namespace Pomodoro.Models
{
    /// <summary>One page of the Todoist API v1 paginated section response.</summary>
    public sealed class TodoistSectionPage
    {
        [JsonPropertyName("results")]
        public List<TodoistSection> Results { get; set; } = new List<TodoistSection>();

        [JsonPropertyName("next_cursor")]
        public string? NextCursor { get; set; }
    }
}
