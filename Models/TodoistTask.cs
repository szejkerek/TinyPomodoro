using System.Text.Json.Serialization;

namespace Pomodoro.Models
{
    public sealed class TodoistTask
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 1;

        [JsonPropertyName("child_order")]
        public int ChildOrder { get; set; }

        [JsonPropertyName("is_completed")]
        public bool IsCompleted { get; set; }
    }
}
