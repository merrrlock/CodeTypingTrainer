namespace CodeTypingTrainer.Models
{
    public enum AccuracyMode
    {
        Cumulative,   // Накопительный — ошибки не прощаются
        CurrentState  // По текущему состоянию — исправил = точность вернулась
    }

    public class TypingSession
    {


        // Количество символов автоматически вставленных программой
        // (закрывающие скобки, отступы после Enter)
        // Вычитается при сравнении с эталоном
        public int AutoInsertedChars { get; set; } = 0;



        // Текущая позиция курсора в InputBox — для мягкого режима точности
        public int CaretPosition { get; set; } = 0;
        public CodeSnippet Snippet { get; set; }
        public DateTime StartTime { get; set; }
        public bool IsActive { get; set; }
        public bool IsPaused { get; set; }

        // Режим подсчёта точности
        public AccuracyMode AccuracyMode { get; set; } = AccuracyMode.CurrentState;

        // Накопительный счётчик (строгий режим)
        public int TotalKeystrokes { get; set; }
        public int CumulativeErrors { get; set; }

        // Для мягкого режима — храним текущий ввод
        public string CurrentInput { get; set; } = "";

        // Словарь ошибок: символ → количество раз ошибся
        public Dictionary<char, int> CharErrors { get; set; } = new();

        // Точность — зависит от выбранного режима
        public double Accuracy
        {
            get
            {
                if (AccuracyMode == AccuracyMode.Cumulative)
                {
                    if (TotalKeystrokes == 0) return 100.0;
                    double correct = TotalKeystrokes - CumulativeErrors;
                    return Math.Round((correct / TotalKeystrokes) * 100, 1);
                }
                else
                {
                    // Мягкий режим — сравниваем только введённую часть
                    // CurrentInput и CaretPosition обновляются из MainWindow
                    if (CaretPosition == 0) return 100.0;
                    string target = Snippet?.Code ?? "";
                    int correct = 0;
                    for (int i = 0; i < CaretPosition && i < CurrentInput.Length && i < target.Length; i++)
                        if (CurrentInput[i] == target[i]) correct++;
                    return Math.Round((double)correct / CaretPosition * 100, 1);
                }
            }
        }

        // WPM считается по введённым символам
        public int WPM
        {
            get
            {
                if (!IsActive) return 0;
                double minutes = (DateTime.Now - StartTime).TotalMinutes;
                if (minutes <= 0.001) return 0;
                int chars = AccuracyMode == AccuracyMode.Cumulative
                    ? TotalKeystrokes
                    : CurrentInput.Length;
                return (int)(chars / 5.0 / minutes);
            }
        }

        // Записать ошибку на конкретный символ
        public void RegisterError(char expectedChar)
        {
            if (CharErrors.ContainsKey(expectedChar))
                CharErrors[expectedChar]++;
            else
                CharErrors[expectedChar] = 1;
        }
    }
}