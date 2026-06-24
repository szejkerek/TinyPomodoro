using System.Text.Json.Serialization;

namespace Pomodoro.Models
{
    /// <summary>The due field of a Todoist task. Only the date is used (for display).</summary>
    public sealed class TodoistDue
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }
    }
}
