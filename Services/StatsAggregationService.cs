using CodeTypingTrainer.Models;
using System.Collections.Generic;
using System.Linq;

namespace CodeTypingTrainer.Services
{
    // Готовит данные из истории сессий и профиля для отображения в UI
    public class StatsAggregationService
    {
        // Точка для графика — одна завершённая сессия
        public class ProgressPoint
        {
            public int SessionIndex { get; set; }
            public double WPM { get; set; }
            public double Accuracy { get; set; }
        }

        public List<ProgressPoint> BuildProgressPoints(List<SessionRecord> sessions, int maxCount = 20)
        {
            var recent = sessions
                .OrderBy(s => s.Timestamp)
                .Skip(System.Math.Max(0, sessions.Count - maxCount))
                .ToList();

            var result = new List<ProgressPoint>();
            for (int i = 0; i < recent.Count; i++)
            {
                result.Add(new ProgressPoint
                {
                    SessionIndex = i + 1,
                    WPM = recent[i].WPM,
                    Accuracy = recent[i].Accuracy
                });
            }
            return result;
        }

        // Возвращает нормализованную интенсивность ошибок (0.0 - 1.0) для каждого символа,
        // относительно символа с максимальным числом ошибок
        public Dictionary<char, double> NormalizeErrorIntensity(Dictionary<char, int> charErrors)
        {
            var result = new Dictionary<char, double>();
            if (charErrors == null || charErrors.Count == 0) return result;

            int max = charErrors.Values.Max();
            if (max == 0) return result;

            foreach (var kvp in charErrors)
                result[kvp.Key] = (double)kvp.Value / max;

            return result;
        }
    }
}