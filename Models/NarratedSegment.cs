namespace CodeTypingTrainer.Models
{
    // Один сегмент строки кода для постепенной озвучки
    public class NarratedSegment
    {
        public string Code { get; set; }    // что должен ввести пользователь, например "def bubble_sort"
        public string Phrase { get; set; }   // что говорит дикторVoice, например "объявить функцию bubble_sort"
    }
}