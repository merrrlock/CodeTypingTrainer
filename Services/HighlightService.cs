using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace CodeTypingTrainer.Services
{
    public class HighlightService
    {
        private static readonly SolidColorBrush _pending =
            new(ColorConverter.ConvertFromString("#6C7086") as Color? ?? Colors.Gray);
        private static readonly SolidColorBrush _correct =
            new(ColorConverter.ConvertFromString("#CDD6F4") as Color? ?? Colors.White);
        private static readonly SolidColorBrush _error =
            new(ColorConverter.ConvertFromString("#F38BA8") as Color? ?? Colors.Red);
        private static readonly SolidColorBrush _cursorBg =
            new(Color.FromArgb(60, 137, 180, 250));

        // Обновлённая сигнатура — принимает caret
        public void UpdateHighlight(TextBlock codeDisplay, string target, string input, int caret)
        {
            codeDisplay.Inlines.Clear();

            for (int i = 0; i < target.Length; i++)
            {
                char targetChar = target[i];
                var run = new Run(targetChar.ToString());

                if (i < caret && i < input.Length)
                {
                    // Эта позиция уже пройдена курсором — оцениваем
                    if (input[i] == targetChar)
                    {
                        run.Foreground = _correct;
                    }
                    else
                    {
                        run.Foreground = _error;
                        run.TextDecorations = TextDecorations.Underline;
                    }
                }
                else if (i == caret)
                {
                    // Показываем символ эталона на позиции курсора — он ещё не введён
                    run.Foreground = new SolidColorBrush(Colors.White);
                    run.Background = _cursorBg;
                }   
                else
                {
                    // Ещё не введено или после курсора (автовставка) — серый
                    run.Foreground = _pending;
                }

                codeDisplay.Inlines.Add(run);
            }
        }

        // Подсветка для режима "На слух" — показывает только введённый текст,
        // без отображения будущих (ещё не напечатанных) символов эталона
        public void UpdateHighlightAudioMode(TextBlock codeDisplay, string expected, string input)
        {
            codeDisplay.Inlines.Clear();

            for (int i = 0; i < input.Length; i++)
            {
                char inputChar = input[i];
                var run = new Run(inputChar.ToString());

                if (i < expected.Length && inputChar == expected[i])
                {
                    run.Foreground = _correct;
                }
                else
                {
                    run.Foreground = _error;
                    run.TextDecorations = TextDecorations.Underline;
                }

                codeDisplay.Inlines.Add(run);
            }

            // Курсор в конце введённого текста
            var cursorRun = new Run("|") { Foreground = _cursorBg == null ? _correct : new SolidColorBrush(Colors.LightBlue) };
            codeDisplay.Inlines.Add(cursorRun);
        }

        public void SetPending(TextBlock codeDisplay, string target)
        {
            codeDisplay.Inlines.Clear();
            codeDisplay.Inlines.Add(new Run(target) { Foreground = _pending });
        }

        public void SetCompleted(TextBlock codeDisplay, string target)
        {
            codeDisplay.Inlines.Clear();
            var brush = new SolidColorBrush(
                ColorConverter.ConvertFromString("#A6E3A1") as Color? ?? Colors.Green);
            codeDisplay.Inlines.Add(new Run(target) { Foreground = brush });
        }
    }
}