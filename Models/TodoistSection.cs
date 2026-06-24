using System.Text.Json.Serialization;

namespace Pomodoro.Models
{
    /// <summary>A Todoist section: a named group of tasks within a project. Shown for information only.</summary>
    public sealed class TodoistSection
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
