using System;

namespace CodeTypingTrainer.Models
{
    // Снимок одной завершённой сессии — сохраняется на диск для истории прогресса
    public class SessionRecord
    {
        public DateTime Timestamp { get; set; }
        public string Language { get; set; }
        public string SnippetTitle { get; set; }
        public string Difficulty { get; set; }
        public int WPM { get; set; }
        public double Accuracy { get; set; }
        public int DurationSeconds { get; set; }
        public int ErrorCount { get; set; }
        public string Mode { get; set; }   // "Normal", "Audio", "Memory"
    }
}