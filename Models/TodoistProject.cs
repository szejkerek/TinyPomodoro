using System.Text.Json.Serialization;

namespace Pomodoro.Models
{
    public sealed class TodoistProject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
