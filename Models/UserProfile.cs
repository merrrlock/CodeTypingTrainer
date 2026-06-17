namespace CodeTypingTrainer.Models
{
    // Данные пользователя, сохраняемые между сессиями
    public class UserProfile
    {
        public string Name { get; set; } = "Пользователь";
        public int TotalSessions { get; set; }       // Всего пройдено сессий
        public int TotalKeystrokes { get; set; }     // Всего нажатий за всё время
        public double BestWPM { get; set; }          // Рекорд скорости
        public double AverageAccuracy { get; set; }  // Средняя точность

        // Словарь: клавиша → количество ошибок на ней
        public Dictionary<string, int> KeyErrors { get; set; } = new();
    }
}