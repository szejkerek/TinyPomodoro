using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Pomodoro.Models
{
    public sealed class TodoistTask : INotifyPropertyChanged
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

        private bool isFocused;
        private bool isCompleting;

        /// <summary>UI-only: the task the user is currently working on (pinned to the top, highlighted).</summary>
        [JsonIgnore]
        public bool IsFocused
        {
            get => isFocused;
            set => SetField(ref isFocused, value, nameof(IsFocused));
        }

        /// <summary>UI-only: a completion is pending (the undo window is open and the row is fading out).</summary>
        [JsonIgnore]
        public bool IsCompleting
        {
            get => isCompleting;
            set => SetField(ref isCompleting, value, nameof(IsCompleting));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetField(ref bool field, bool value, string propertyName)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
