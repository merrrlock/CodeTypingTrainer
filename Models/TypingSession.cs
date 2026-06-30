using System;
using System.Collections.Generic;

namespace CodeTypingTrainer.Models
{
    public class TypingSession
    {
        public CodeSnippet Snippet { get; set; }
        public DateTime StartTime { get; set; }
        public bool IsActive { get; set; }
        public bool IsPaused { get; set; }

        public int TotalKeystrokes { get; set; }

        // Позиции в эталоне, на которых хотя бы раз была допущена ошибка.
        // Используем HashSet — позиция добавляется один раз и навсегда остаётся учтённой,
        // даже если пользователь потом исправил символ на этом месте.
        public HashSet<int> ErrorPositions { get; set; } = new();

        public int CumulativeErrors => ErrorPositions.Count;

        public string CurrentInput { get; set; } = "";
        public int CaretPosition { get; set; } = 0;

        public Dictionary<char, int> CharErrors { get; set; } = new();

        public double Accuracy
        {
            get
            {
                if (TotalKeystrokes == 0) return 100.0;
                double correct = TotalKeystrokes - CumulativeErrors;
                if (correct < 0) correct = 0;
                return Math.Round((correct / TotalKeystrokes) * 100, 1);
            }
        }

        public int WPM
        {
            get
            {
                if (!IsActive) return 0;
                double minutes = (DateTime.Now - StartTime).TotalMinutes;
                if (minutes <= 0.001) return 0;
                return (int)(CaretPosition / 5.0 / minutes);
            }
        }

        // Регистрирует ошибку на конкретной позиции эталона.
        // Если позиция уже была отмечена ранее — ничего не меняется (не дублируем).
        public void RegisterErrorAtPosition(int position, char expectedChar)
        {
            if (ErrorPositions.Add(position))
            {
                // Add() возвращает true только если элемент был новым —
                // значит в CharErrors тоже добавляем только при первой ошибке на этой позиции
                if (CharErrors.ContainsKey(expectedChar))
                    CharErrors[expectedChar]++;
                else
                    CharErrors[expectedChar] = 1;
            }
        }
    }
}