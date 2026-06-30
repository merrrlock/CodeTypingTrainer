using System.Collections.Generic;

namespace CodeTypingTrainer.Models
{
    public class UserProfile
    {
        public string Name { get; set; } = "Пользователь";
        public int TotalSessions { get; set; }
        public int TotalKeystrokes { get; set; }
        public double BestWPM { get; set; }
        public double AverageAccuracy { get; set; }

        // Словарь: символ → количество ошибок на нём (накопительно, для тепловой карты)
        public Dictionary<char, int> CharErrors { get; set; } = new();

        // Обновить профиль данными новой завершённой сессии
        public void ApplySession(SessionRecord record, int keystrokes, Dictionary<char, int> sessionErrors)
        {
            TotalSessions++;
            TotalKeystrokes += keystrokes;

            if (record.WPM > BestWPM)
                BestWPM = record.WPM;

            // Пересчитываем среднюю точность как скользящее среднее
            AverageAccuracy = ((AverageAccuracy * (TotalSessions - 1)) + record.Accuracy) / TotalSessions;

            foreach (var kvp in sessionErrors)
            {
                if (CharErrors.ContainsKey(kvp.Key))
                    CharErrors[kvp.Key] += kvp.Value;
                else
                    CharErrors[kvp.Key] = kvp.Value;
            }
        }
    }
}