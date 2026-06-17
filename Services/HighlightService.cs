using System;
using System.Windows.Controls;

namespace CodeTypingTrainer.Services
{
    public class HighlightService
    {
        // Заглушка — реализация не критична для анализа
        public void SetPending(TextBlock tb, string code) { tb.Text = code; }
        public void SetCompleted(TextBlock tb, string code) { tb.Text = code; }
        public void UpdateHighlight(TextBlock tb, string target, string input, int caret) { tb.Text = target; }
        public void UpdateHighlightAudioMode(TextBlock tb, string expected, string input) { tb.Text = expected; }
    }
}