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

        [JsonPropertyName("section_id")]
        public string? SectionId { get; set; }

        [JsonPropertyName("due")]
        public TodoistDue? Due { get; set; }

        private bool isFocused;
        private bool isCompleting;
        private string status = string.Empty;
        private string sectionName = string.Empty;
        private string dueDate = string.Empty;

        /// <summary>UI-only: the backend status/column this task sits in (ClickUp), e.g. "in progress". Empty otherwise.</summary>
        [JsonIgnore]
        public string Status
        {
            get => status;
            set => SetField(ref status, value, nameof(Status));
        }

        /// <summary>UI-only: the Todoist section this task sits in, shown for information. Empty if none.</summary>
        [JsonIgnore]
        public string SectionName
        {
            get => sectionName;
            set => SetField(ref sectionName, value, nameof(SectionName));
        }

        /// <summary>UI-only: short due-date label (e.g. "📅 Jun 26"), or empty when the task has no due date.</summary>
        [JsonIgnore]
        public string DueDate
        {
            get => dueDate;
            set => SetField(ref dueDate, value, nameof(DueDate));
        }

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

        private void SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
