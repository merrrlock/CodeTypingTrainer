namespace CodeTypingTrainer.Models
{
    // Один фрагмент кода из библиотеки заданий
    public class CodeSnippet
    {
        public string Title { get; set; }        // Название, например "Сортировка пузырьком"
        public string Language { get; set; }     // Python, JavaScript и т.д.
        public string Category { get; set; }     // Алгоритмы, Структуры данных...
        public string Difficulty { get; set; }   // Легко / Средне / Сложно
        public string Code { get; set; }         // Сам код для набора
    }
}