using CodeTypingTrainer.Models;
using CodeTypingTrainer.Services;
using CodeTypingTrainer.Services.CodeNarrators;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace CodeTypingTrainer
{
    public partial class MainWindow : Window
    {
        private ConfigService _config;
        private readonly GoogleTtsService _googleTts;

        private readonly SpeechService _speech = new();
        private readonly NarratorRegistry _narrators = new();

        // Состояние режима "На слух"
        private List<string> _audioLines = new();
        private int _currentAudioLine = 0;
        private List<Models.NarratedSegment> _currentSegments = new();
        private int _currentSegmentIndex = 0;
        private string _segmentsTypedSoFar = "";   // что введено в рамках текущей строки (все пройденные сегменты + текущий ввод)
        private bool _isAudioMode = false;

        private string _userInput = "";
        private readonly HighlightService _highlight = new();
        private readonly SnippetLibrary _library = new();
        private readonly LanguageRegistry _languageRegistry = new();
        private readonly AutoCompleteService _autoComplete;
        private TypingSession _session;

        private AccuracyMode _accuracyMode = AccuracyMode.CurrentState;
        private bool _autoCompleteEnabled = true;
        private bool _suggestionsEnabled = true;
        private string _currentLanguage = "Python";

        private readonly DispatcherTimer _timer = new()
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        public MainWindow()
{
    InitializeComponent();
    _autoComplete = new AutoCompleteService(_languageRegistry);

    try
    {
        _config = new ConfigService();
        _googleTts = new GoogleTtsService(_config.GetGoogleTtsApiKey());
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            $"Не удалось загрузить конфигурацию озвучки:\n{ex.Message}\n\nРежим «На слух» будет недоступен.",
            "Ошибка конфигурации",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    _timer.Tick += Timer_Tick;
}

        // -------------------------------------------------------
        // СТАРТ
        // -------------------------------------------------------
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            _currentLanguage = (LanguageCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Python";

            if (_isAudioMode && !_narrators.IsSupported(_currentLanguage))
            {
                StatusText.Text = $"Режим «На слух» пока не поддерживает {_currentLanguage}. Выберите Python.";
                return;
            }

            var snippet = _library.GetRandom(_currentLanguage);
            if (snippet == null)
            {
                StatusText.Text = "Фрагменты не найдены.";
                return;
            }

            _session = new TypingSession
            {
                Snippet = snippet,
                StartTime = DateTime.Now,
                IsActive = true,
                AccuracyMode = _accuracyMode
            };

            _userInput = "";
            InputBox.Clear();
            InputBox.IsEnabled = true;
            InputBox.Focus();

            StartButton.IsEnabled = false;
            PauseButton.IsEnabled = true;
            RestartButton.IsEnabled = true;

            if (_isAudioMode)
            {
                StartAudioMode(snippet);
            }
            else
            {
                CodeDisplay.Text = null;
                _highlight.SetPending(CodeDisplay, snippet.Code);
            }

            _timer.Start();
            StatusText.Text = _isAudioMode
                ? $"Слушайте и печатайте: {snippet.Title}"
                : $"Набираем: {snippet.Title}  |  {snippet.Language}  |  {snippet.Difficulty}";
        }

        // -------------------------------------------------------
        // РЕЖИМ "НА СЛУХ" — построчная озвучка по сегментам
        // -------------------------------------------------------
        private void StartAudioMode(CodeSnippet snippet)
        {
            var narrator = _narrators.Get(snippet.Language);
            _audioLines = narrator.SplitIntoLines(snippet.Code);
            _currentAudioLine = 0;

            CodeDisplay.Inlines.Clear();
            CodeDisplay.Text = "🔊 Слушайте и печатайте. Эталон скрыт до завершения.";

            ShowCodeButton.IsEnabled = false;

            LoadCurrentLineSegments();
        }

        private void LoadCurrentLineSegments()
        {
            if (_currentAudioLine >= _audioLines.Count)
            {
                FinishSession();
                return;
            }

            var narrator = _narrators.Get(_currentLanguage);
            string line = _audioLines[_currentAudioLine];

            _currentSegments = narrator.NarrateLineSegments(line);
            _currentSegmentIndex = 0;
            _segmentsTypedSoFar = "";

            InputBox.Clear();
            _userInput = "";

            SpeakCurrentSegment();
        }

        private async void SpeakCurrentSegment()
        {
            if (_currentSegmentIndex >= _currentSegments.Count) return;

            string phrase = _currentSegments[_currentSegmentIndex].Phrase;
            await PlayPhraseAsync(phrase);
        }

        private async System.Threading.Tasks.Task PlayPhraseAsync(string phrase)
        {
            StatusText.Text = "🔊 Озвучиваю...";
            var (audioData, error) = await _googleTts.SynthesizeAsync(phrase);

            if (error != null)
            {
                StatusText.Text = $"Ошибка озвучки: {error}";
                return;
            }

            byte[] wavData = AddWavHeader(audioData, 24000, 1, 16);
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tts_segment.wav");
            System.IO.File.WriteAllBytes(tempPath, wavData);

            var player = new System.Media.SoundPlayer(tempPath);
            player.Play();

            StatusText.Text = $"Печатайте: \"{_currentSegments[_currentSegmentIndex].Code.Trim()}\"";
        }

        private void ReplayLineButton_Click(object sender, RoutedEventArgs e)
        {
            SpeakCurrentSegment();
        }

        private void NextLineButton_Click(object sender, RoutedEventArgs e)
        {
            // Пропустить текущую строку целиком — переходим к следующей
            _currentAudioLine++;
            LoadCurrentLineSegments();
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _speech.SpeedMultiplier = e.NewValue;
        }

        private void ShowCodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null) return;
            _highlight.SetPending(CodeDisplay, _session.Snippet.Code);
        }

        // -------------------------------------------------------
        // ПЕРЕХВАТ КЛАВИШ — автодополнение и автотабуляция
        // -------------------------------------------------------
        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // --- Tab: принять подсказку или вставить отступ ---
            if (e.Key == Key.Tab)
            {
                if (SuggestionsPopup.IsOpen && SuggestionsList.Items.Count > 0)
                {
                    AcceptSuggestion();
                }
                else
                {
                    // Проверяем: курсор стоит прямо перед закрывающей скобкой
                    int caret = InputBox.CaretIndex;
                    string text = InputBox.Text;

                    bool isBeforeClosing = caret < text.Length && (
                        text[caret] == ')' ||
                        text[caret] == ']' ||
                        text[caret] == '}');

                    if (isBeforeClosing)
                        InputBox.CaretIndex = caret + 1;
                    else
                        InsertAtCaret(_currentLanguage == "Go" ? "\t" : "    ");
                }
                e.Handled = true;
                return;
            }

            // --- Escape: закрыть подсказки ---
            if (e.Key == Key.Escape)
            {
                SuggestionsPopup.IsOpen = false;
                e.Handled = true;
                return;
            }

            // --- Стрелка вниз: перейти в список подсказок ---
            if (e.Key == Key.Down && SuggestionsPopup.IsOpen)
            {
                SuggestionsList.Focus();
                if (SuggestionsList.Items.Count > 0)
                    SuggestionsList.SelectedIndex = 0;
                e.Handled = true;
                return;
            }

            if (!_autoCompleteEnabled) return;

            // --- Enter: автотабуляция ---
            if (e.Key == Key.Enter)
            {
                // Shift+Enter перед ) — выход из скобки на новую строку
                if (IsShift(e))
                {
                    int caret = InputBox.CaretIndex;
                    string text = InputBox.Text;

                    if (caret < text.Length && (text[caret] == ')' || text[caret] == ']' || text[caret] == '}'))
                    {
                        // Определяем отступ текущей строки
                        int lineStart = text.LastIndexOf('\n', Math.Max(0, caret - 1)) + 1;
                        string currentLine = text.Substring(lineStart, caret - lineStart);

                        int indent = 0;
                        foreach (char c in currentLine)
                        {
                            if (c == ' ') indent++;
                            else if (c == '\t') indent += 4;
                            else break;
                        }

                        // Вставляем перенос ПОСЛЕ закрывающей скобки с тем же отступом
                        var rules = _languageRegistry.Get(_currentLanguage);
                        string indentStr = rules.IndentChar == "tab"
                            ? new string('\t', indent / 4)
                            : new string(' ', indent);

                        string insertion = $"\n{indentStr}";

                        InputBox.TextChanged -= InputBox_TextChanged;
                        // Курсор сдвигаем за ) и вставляем новую строку после неё
                        string before = text.Substring(0, caret + 1); // включая )
                        string after = text.Substring(caret + 1);
                        InputBox.Text = before + insertion + after;
                        InputBox.CaretIndex = caret + 1 + insertion.Length;
                        InputBox.TextChanged += InputBox_TextChanged;

                        if (_session != null)
                        {
                            string input = InputBox.Text;
                            caret = InputBox.CaretIndex;

                            _session.CurrentInput = input;
                            _session.CaretPosition = caret;

                            string target = _session.Snippet.Code;
                            string userTyped = input.Substring(0, Math.Min(caret, input.Length));

                            if (_session.AccuracyMode == AccuracyMode.Cumulative)
                            {
                                _session.TotalKeystrokes = userTyped.Length;
                                int errors = 0;
                                for (int i = 0; i < userTyped.Length && i < target.Length; i++)
                                    if (userTyped[i] != target[i]) errors++;
                                if (errors > _session.CumulativeErrors)
                                {
                                    if (userTyped.Length <= target.Length)
                                        _session.RegisterError(target[userTyped.Length - 1]);
                                    _session.CumulativeErrors = errors;
                                }
                            }

                            AccuracyText.Text = $"{_session.Accuracy}%";
                            _highlight.UpdateHighlight(CodeDisplay, target, input, caret);
                            ScrollToCurrentPosition(caret);
                        }

                        e.Handled = true;
                    }
                    // Если курсор не перед ) — Shift+Enter ведёт себя как обычный Enter
                    else
                    {
                        HandleEnterKey(e);
                    }
                    return;
                }

                HandleEnterKey(e);
                return;
            }

            // --- Backspace: умное удаление ---
            if (e.Key == Key.Back)
            {
                HandleBackspaceKey(e);
                return;
            }

            // --- Перепрыгивание через закрывающий символ ---
            HandleJumpOver(e);
        }

        private void HandleEnterKey(KeyEventArgs e)
        {
            int caretPos = InputBox.CaretIndex;
            string text = InputBox.Text;

            int lineStart = text.LastIndexOf('\n', Math.Max(0, caretPos - 1)) + 1;
            string currentLine = text.Substring(lineStart, caretPos - lineStart);

            int indent = 0;
            foreach (char c in currentLine)
            {
                if (c == ' ') indent++;
                else if (c == '\t') indent += 4;
                else break;
            }

            var rules = _languageRegistry.Get(_currentLanguage);
            string indentUnit = rules.IndentChar == "tab" ? "\t" : new string(' ', rules.IndentSize);
            string currentIndent = rules.IndentChar == "tab"
                ? new string('\t', indent / 4)
                : new string(' ', indent);

            bool cursorBetweenBraces = rules.UseBraceBlocks
                && caretPos > 0
                && caretPos < text.Length
                && text[caretPos - 1] == '{'
                && text[caretPos] == '}';

            string insertion;
            int autoInserted = 0;

            if (cursorBetweenBraces)
            {
                string inner = currentIndent + indentUnit;
                string outer = currentIndent;
                string before = text.Substring(0, caretPos);
                string after = text.Substring(caretPos + 1);

                // Автовставка: "\n" + outer + "}" — это символы добавленные программой
                // "\n" + inner — это то что пользователь ввёл бы сам (Enter + отступ)
                // Программа вставила: "\n" + outer + "}" = outer.Length + 2 символа
                autoInserted = outer.Length + 2;

                // Минус один: закрывающую } из автопары тоже убираем из счётчика
                // (она уже была посчитана в HandleAutoPair)
                if (_session != null) _session.AutoInsertedChars--;

                insertion = $"\n{inner}\n{outer}}}";

                InputBox.TextChanged -= InputBox_TextChanged;
                InputBox.Text = before + insertion + after;
                InputBox.CaretIndex = caretPos + 1 + inner.Length;
                InputBox.TextChanged += InputBox_TextChanged;
            }
            else if (rules.ShouldIncreaseIndent(currentLine))
            {
                string newIndentStr = $"{currentIndent}{indentUnit}";

                if (rules.UseBraceBlocks)
                {
                    // Вставляем: "\n" + inner + "\n" + outer + "}"
                    // Пользователь сам ввёл бы: "\n" + inner
                    // Программа добавила: "\n" + outer + "}" 
                    autoInserted = currentIndent.Length + 2;
                    insertion = $"\n{newIndentStr}\n{currentIndent}}}";
                }
                else
                {
                    // Python: программа добавляет только отступ
                    // Пользователь сам нажал Enter, отступ — автовставка
                    autoInserted = newIndentStr.Length + 1; // +1 за \n
                                                            // Но \n пользователь тоже вводит — вычитаем его обратно
                    autoInserted = newIndentStr.Length; // только пробелы отступа
                    insertion = $"\n{newIndentStr}";
                }

                InputBox.TextChanged -= InputBox_TextChanged;
                int newCaret = caretPos + 1 + newIndentStr.Length;
                InputBox.Text = text.Substring(0, caretPos) + insertion + text.Substring(caretPos);
                InputBox.CaretIndex = newCaret;
                InputBox.TextChanged += InputBox_TextChanged;
            }
            else if (rules.ShouldDecreaseIndent(currentLine))
            {
                int newIndent = Math.Max(0, indent - rules.IndentSize);
                string newIndentStr = new string(' ', newIndent);
                // Отступ уменьшен — программа не добавляет ничего лишнего,
                // но и пробелы отступа нужно засчитать как автовставку
                autoInserted = newIndentStr.Length;
                insertion = $"\n{newIndentStr}";

                InputBox.TextChanged -= InputBox_TextChanged;
                InputBox.Text = text.Substring(0, caretPos) + insertion + text.Substring(caretPos);
                InputBox.CaretIndex = caretPos + insertion.Length;
                InputBox.TextChanged += InputBox_TextChanged;
            }
            else
            {
                // Обычный Enter — программа добавляет только отступ текущей строки
                autoInserted = currentIndent.Length;
                insertion = $"\n{currentIndent}";

                InputBox.TextChanged -= InputBox_TextChanged;
                InputBox.Text = text.Substring(0, caretPos) + insertion + text.Substring(caretPos);
                InputBox.CaretIndex = caretPos + insertion.Length;
                InputBox.TextChanged += InputBox_TextChanged;
            }

            if (_session != null) _session.AutoInsertedChars += autoInserted;

            e.Handled = true;


            if (_session != null)
            {
                string input = InputBox.Text;
                int caret = InputBox.CaretIndex;

                // После Enter автовставленных символов после курсора нет —
                // курсор стоит в конце введённого, всё до него введено пользователем
                _userInput = input.Substring(0, Math.Min(caret, input.Length));

                _session.CurrentInput = _userInput;
                _session.CaretPosition = _userInput.Length;

                string target = _session.Snippet.Code;

                if (_session.AccuracyMode == AccuracyMode.Cumulative)
                {
                    _session.TotalKeystrokes = _userInput.Length;
                    int errors = 0;
                    for (int i = 0; i < _userInput.Length && i < target.Length; i++)
                        if (_userInput[i] != target[i]) errors++;
                    if (errors > _session.CumulativeErrors)
                    {
                        if (_userInput.Length <= target.Length)
                            _session.RegisterError(target[_userInput.Length - 1]);
                        _session.CumulativeErrors = errors;
                    }
                }

                AccuracyText.Text = $"{_session.Accuracy}%";
                _highlight.UpdateHighlight(CodeDisplay, target, _userInput, _userInput.Length);
                ScrollToCurrentPosition(_userInput.Length);
            }
        }

        // -------------------------------------------------------
        // УМНЫЙ BACKSPACE
        // -------------------------------------------------------
        private void HandleBackspaceKey(KeyEventArgs e)
        {
            int caret = InputBox.CaretIndex;
            string text = InputBox.Text;

            if (caret == 0) return; // нечего удалять

            // Случай 1: курсор внутри пустой пары — удаляем оба символа
            // Например: (|) или "|" или [|]
            if (caret < text.Length)
            {
                char before = text[caret - 1];
                char after = text[caret];

                bool isPair = (before == '(' && after == ')')
                           || (before == '[' && after == ']')
                           || (before == '"' && after == '"')
                           || (before == '\'' && after == '\'')
                           || (before == '{' && after == '}' && _currentLanguage != "Python");

                if (isPair)
                {
                    InputBox.TextChanged -= InputBox_TextChanged;
                    InputBox.Text = text.Substring(0, caret - 1) + text.Substring(caret + 1);
                    InputBox.CaretIndex = caret - 1;
                    InputBox.TextChanged += InputBox_TextChanged;

                    // Обновляем сессию вручную
                    if (_session != null) _session.CurrentInput = InputBox.Text;
                    AccuracyText.Text = $"{_session?.Accuracy ?? 100}%";

                    e.Handled = true;
                    return;
                }
            }

            // Случай 2: курсор в начале строки или после чистого отступа —
            // удаляем один уровень отступа (4 пробела или 1 таб)
            int lineStart = text.LastIndexOf('\n', caret - 1) + 1;
            string beforeCaret = text.Substring(lineStart, caret - lineStart);
            bool isOnlyIndent = beforeCaret.Length > 0 && beforeCaret.Trim().Length == 0;

            if (isOnlyIndent)
            {
                int indentUnit = _currentLanguage == "Go" ? 1 : 4;

                // Сколько символов отступа удалить — не больше одного уровня
                int toDelete = Math.Min(indentUnit, beforeCaret.Length);

                InputBox.TextChanged -= InputBox_TextChanged;
                InputBox.Text = text.Substring(0, caret - toDelete) + text.Substring(caret);
                InputBox.CaretIndex = caret - toDelete;
                InputBox.TextChanged += InputBox_TextChanged;

                if (_session != null) _session.CurrentInput = InputBox.Text;
                AccuracyText.Text = $"{_session?.Accuracy ?? 100}%";

                e.Handled = true;
                return;
            }

            // Случай 3: обычное удаление — не перехватываем, WPF сделает сам
        }

        // -------------------------------------------------------
        // ПЕРЕПРЫГИВАНИЕ ЧЕРЕЗ ЗАКРЫВАЮЩИЙ СИМВОЛ
        // -------------------------------------------------------
        private void HandleJumpOver(KeyEventArgs e)
        {
            int caret = InputBox.CaretIndex;
            string text = InputBox.Text;
            if (caret >= text.Length) return;

            char next = text[caret];
            bool shift = IsShift(e);

            // Определяем символ, который пользователь намерен ввести
            char? pressed = e.Key switch
            {
                Key.D0 when shift => ')',   // Shift+0 = )
                Key.OemCloseBrackets when shift => '}',
                Key.OemCloseBrackets => ']',
                Key.OemQuotes when shift => '"',   // Shift+" = "
                Key.OemQuotes => '\'',  // ' без Shift = '
                _ => null
            };

            if (pressed == null) return;

            // Если следующий символ уже является нажатым — перепрыгиваем
            if (next == pressed.Value)
            {
                InputBox.CaretIndex = caret + 1;
                e.Handled = true;
            }
        }

        // Вспомогательный метод: проверка зажатого Shift
        private static bool IsShift(KeyEventArgs e) =>
            (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        // -------------------------------------------------------
        // ВСТАВКА ТЕКСТА В ПОЗИЦИЮ КУРСОРА
        // -------------------------------------------------------
        private void InsertAtCaret(string insertion)
        {
            int caret = InputBox.CaretIndex;
            string text = InputBox.Text;

            InputBox.TextChanged -= InputBox_TextChanged;
            InputBox.Text = text.Substring(0, caret) + insertion + text.Substring(caret);
            InputBox.CaretIndex = caret + insertion.Length;
            InputBox.TextChanged += InputBox_TextChanged;

            if (_session != null)
            {
                string input = InputBox.Text;
                int newCaret = InputBox.CaretIndex;
                string target = _session.Snippet.Code;

                _userInput = input.Substring(0, Math.Min(newCaret, input.Length));

                _session.CurrentInput = _userInput;
                _session.CaretPosition = _userInput.Length;

                AccuracyText.Text = $"{_session.Accuracy}%";
                _highlight.UpdateHighlight(CodeDisplay, target, _userInput, _userInput.Length);
                ScrollToCurrentPosition(_userInput.Length);
            }
        }

        // -------------------------------------------------------
        // ВВОД ТЕКСТА
        // -------------------------------------------------------
        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_session == null || !_session.IsActive || _session.IsPaused) return;

            if (_isAudioMode)
            {
                HandleAudioModeInput();
                return;
            }

            string target = _session.Snippet.Code;

            if (_autoCompleteEnabled)
                HandleAutoPair(InputBox.Text);

            string input = InputBox.Text;
            int caret = InputBox.CaretIndex;

            // _userInput = только символы до курсора — автовставки стоят после курсора
            _userInput = input.Substring(0, Math.Min(caret, input.Length));

            _session.CurrentInput = _userInput;
            _session.CaretPosition = _userInput.Length;

            // Точность считаем по _userInput
            if (_session.AccuracyMode == AccuracyMode.Cumulative)
            {
                _session.TotalKeystrokes = _userInput.Length;
                int errors = 0;
                for (int i = 0; i < _userInput.Length && i < target.Length; i++)
                    if (_userInput[i] != target[i]) errors++;
                if (errors > _session.CumulativeErrors)
                {
                    if (_userInput.Length <= target.Length)
                        _session.RegisterError(target[_userInput.Length - 1]);
                    _session.CumulativeErrors = errors;
                }
            }

            AccuracyText.Text = $"{_session.Accuracy}%";

            // Подсветка — передаём _userInput и его длину как позицию курсора
            _highlight.UpdateHighlight(CodeDisplay, target, _userInput, _userInput.Length);
            ScrollToCurrentPosition(_userInput.Length);

            if (_suggestionsEnabled)
                UpdateSuggestions(input);

            // Завершение — сравниваем _userInput с эталоном
            if (_userInput.Length >= target.Length && _userInput == target)
                FinishSession();
        }

        private void HandleAudioModeInput()
        {
            if (_currentSegmentIndex >= _currentSegments.Count) return;

            string targetSegmentCode = _currentSegments[_currentSegmentIndex].Code;
            string fullExpectedSoFar = _segmentsTypedSoFar + targetSegmentCode;

            string input = InputBox.Text;

            // Подсветка — показываем только то что введено, сравнивая с ожидаемым текстом
            _highlight.UpdateHighlightAudioMode(CodeDisplay, fullExpectedSoFar, input);

            // Считаем точность по всему накопленному вводу в рамках строки
            int correct = 0;
            int compareLen = Math.Min(input.Length, fullExpectedSoFar.Length);
            for (int i = 0; i < compareLen; i++)
                if (input[i] == fullExpectedSoFar[i]) correct++;

            if (input.Length > 0)
                AccuracyText.Text = $"{Math.Round((double)correct / input.Length * 100, 1)}%";

            // Проверяем — дошёл ли пользователь до конца текущего сегмента
            if (input.Length >= fullExpectedSoFar.Length && input == fullExpectedSoFar)
            {
                // Сегмент завершён — переходим к следующему
                _segmentsTypedSoFar = fullExpectedSoFar;
                _currentSegmentIndex++;

                if (_currentSegmentIndex < _currentSegments.Count)
                {
                    // Есть следующий сегмент этой строки — озвучиваем его
                    SpeakCurrentSegment();
                }
                else
                {
                    // Строка закончена — переходим к следующей строке
                    _currentAudioLine++;
                    LoadCurrentLineSegments();
                }
            }
        }

        // Извлекаем «чистый» ввод — убираем автовставленные символы в конце
        private string GetCleanInput(string input, string target)
        {
            // Идём посимвольно по эталону и собираем совпадающую часть
            // Автовставленные символы совпадают с эталоном, поэтому
            // нам достаточно просто взять первые target.Length символов
            if (input.Length >= target.Length)
                return input.Substring(0, target.Length);
            return input;
        }

        private void HandleAutoPair(string input)
        {
            int caret = InputBox.CaretIndex;
            if (caret == 0 || caret > input.Length) return;

            char lastChar = input[caret - 1];

            if (_autoComplete.TryGetPair(lastChar, _currentLanguage, out char closing))
            {
                if (caret < input.Length && input[caret] == closing) return;

                string before = input.Substring(0, caret);
                string after = input.Substring(caret);

                InputBox.TextChanged -= InputBox_TextChanged;
                InputBox.Text = before + closing + after;
                InputBox.CaretIndex = caret;
                InputBox.TextChanged += InputBox_TextChanged;
                // _userInput НЕ меняем — закрывающий символ вставлен программой
            }
        }

        private void ScrollToCurrentPosition(int inputLength)
        {
            if (_session == null) return;
            string target = _session.Snippet.Code;
            if (inputLength >= target.Length) return;

            // Считаем на какой строке находится текущий символ
            string typed = target.Substring(0, inputLength);
            int currentLine = 0;
            foreach (char c in typed)
                if (c == '\n') currentLine++;

            // Считаем общее количество строк
            int totalLines = 1;
            foreach (char c in target)
                if (c == '\n') totalLines++;

            // Прокручиваем пропорционально
            double ratio = totalLines > 1 ? (double)currentLine / (totalLines - 1) : 0;
            double maxOffset = CodeScroller.ScrollableHeight;
            CodeScroller.ScrollToVerticalOffset(ratio * maxOffset);
        }

        private void UpdateSuggestions(string input)
        {
            // Найдём текущее слово (от последнего пробела/перевода строки)
            int caret = InputBox.CaretIndex;
            if (caret == 0) { SuggestionsPopup.IsOpen = false; return; }

            int wordStart = caret - 1;
            while (wordStart > 0 && char.IsLetterOrDigit(input[wordStart - 1]))
                wordStart--;

            string currentWord = input.Substring(wordStart, caret - wordStart);
            var suggestions = _autoComplete.GetSuggestions(currentWord, _currentLanguage);

            if (suggestions.Count > 0)
            {
                SuggestionsList.ItemsSource = suggestions;
                SuggestionsPopup.IsOpen = true;
            }
            else
            {
                SuggestionsPopup.IsOpen = false;
            }
        }

        private void AcceptSuggestion()
        {
            if (SuggestionsList.SelectedIndex < 0 && SuggestionsList.Items.Count > 0)
                SuggestionsList.SelectedIndex = 0;

            string selected = SuggestionsList.SelectedItem as string;
            if (selected == null) return;

            int caret = InputBox.CaretIndex;
            string text = InputBox.Text;

            // Находим начало текущего слова — идём назад от курсора
            int wordStart = caret;
            while (wordStart > 0 && char.IsLetterOrDigit(text[wordStart - 1]))
                wordStart--;

            // Заменяем текущее слово на выбранную подсказку
            string before = text.Substring(0, wordStart);
            string after = text.Substring(caret);

            InputBox.TextChanged -= InputBox_TextChanged;
            InputBox.Text = before + selected + after;
            InputBox.CaretIndex = wordStart + selected.Length;
            InputBox.TextChanged += InputBox_TextChanged;

            SuggestionsPopup.IsOpen = false;

            // Обновляем сессию и подсветку вручную
            if (_session != null)
            {
                string input = InputBox.Text;
                int newCaret = InputBox.CaretIndex;
                string target = _session.Snippet.Code;

                _session.CurrentInput = input;
                _session.CaretPosition = newCaret;

                AccuracyText.Text = $"{_session.Accuracy}%";
                _highlight.UpdateHighlight(CodeDisplay, target, input, newCaret);
                ScrollToCurrentPosition(newCaret);
            }
        }

        // Выбор подсказки двойным кликом
        private void SuggestionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            AcceptSuggestion();
            InputBox.Focus();
        }

        // Выбор подсказки клавишей Enter в списке
        private void SuggestionsList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                AcceptSuggestion();
                InputBox.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                SuggestionsPopup.IsOpen = false;
                InputBox.Focus();
            }
        }

        // -------------------------------------------------------
        // ЗАВЕРШЕНИЕ
        // -------------------------------------------------------
        private void FinishSession()
        {
            _timer.Stop();
            _session.IsActive = false;
            InputBox.IsEnabled = false;
            PauseButton.IsEnabled = false;
            SuggestionsPopup.IsOpen = false;
            _speech.StopAll();

            if (_isAudioMode)
                ShowCodeButton.IsEnabled = true;

            _highlight.SetCompleted(CodeDisplay, _session.Snippet.Code);

            TimeSpan elapsed = DateTime.Now - _session.StartTime;
            StatusText.Text = $"✅ Готово!  WPM: {_session.WPM}  |  Точность: {_session.Accuracy}%  |  Время: {elapsed.Minutes}:{elapsed.Seconds:D2}";
            StartButton.IsEnabled = true;
        }

        // -------------------------------------------------------
        // ПАУЗА
        // -------------------------------------------------------
        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null) return;
            if (!_session.IsPaused)
            {
                _session.IsPaused = true;
                _timer.Stop();
                InputBox.IsEnabled = false;
                SuggestionsPopup.IsOpen = false;
                PauseButton.Content = "▶ Продолжить";
                StatusText.Text = "Пауза.";
            }
            else
            {
                _session.IsPaused = false;
                _timer.Start();
                InputBox.IsEnabled = true;
                InputBox.Focus();
                PauseButton.Content = "⏸ Пауза";
                StatusText.Text = $"Набираем: {_session.Snippet.Title}";
            }
        }

        // -------------------------------------------------------
        // ПЕРЕЗАПУСК
        // -------------------------------------------------------
        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            _speech.StopAll();
            _timer.Stop();
            // Сначала сохраняем код текущего фрагмента (если есть), затем обнуляем сессию
            string snippetCode = _session?.Snippet?.Code ?? string.Empty;
            _session = null;
            SuggestionsPopup.IsOpen = false;

            InputBox.Clear();
            InputBox.IsEnabled = false;
            WpmText.Text = "0";
            AccuracyText.Text = "100%";
            TimerText.Text = "0:00";
            CodeDisplay.Inlines.Clear();
            CodeDisplay.Text = "Нажмите «Старт» для начала тренировки";

            StartButton.IsEnabled = true;
            PauseButton.IsEnabled = false;
            PauseButton.Content = "⏸ Пауза";
            RestartButton.IsEnabled = false;

            // Если был код фрагмента — отобразим его как завершённый, иначе оставим подсказку
            if (!string.IsNullOrEmpty(snippetCode))
                _highlight.SetCompleted(CodeDisplay, snippetCode);
            StatusText.Text = "Готов к работе.";
        }

        // -------------------------------------------------------
        // ТАЙМЕР
        // -------------------------------------------------------
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_session == null) return;
            TimeSpan elapsed = DateTime.Now - _session.StartTime;
            TimerText.Text = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
            WpmText.Text = _session.WPM.ToString();
        }

        // -------------------------------------------------------
        // СМЕНА ЯЗЫКА И РЕЖИМА
        // -------------------------------------------------------
        private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentLanguage = (LanguageCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Python";
            if (_session?.IsActive == true) RestartButton_Click(null, null);
        }

        private void ModeButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string mode = btn?.Tag?.ToString();

            _isAudioMode = mode == "Audio";

            AudioControlsPanel.Visibility = _isAudioMode ? Visibility.Visible : Visibility.Collapsed;

            string modeText = mode switch
            {
                "Normal" => "Обычный режим",
                "Memory" => "По памяти (скоро)",
                "Audio" => "На слух",
                _ => "Неизвестный режим"
            };
            StatusText.Text = $"Выбран режим: {modeText}. Нажмите «Старт».";
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: переключение темы
        }

        private async void TestTts_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Отправляю запрос к Google TTS...";

            var (audioData, error) = await _googleTts.SynthesizeAsync("Привет, это тест синтеза речи");

            if (error != null)
            {
                StatusText.Text = $"Ошибка: {error}";
                MessageBox.Show(error, "Ошибка TTS", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StatusText.Text = $"Успех! Получено {audioData.Length} байт аудио.";

            // Сохраняем во временный файл и проигрываем системным плеером
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tts_test.wav");

            // Gemini TTS возвращает PCM 24kHz 16-bit mono — нужно добавить WAV-заголовок
            byte[] wavData = AddWavHeader(audioData, 24000, 1, 16);
            System.IO.File.WriteAllBytes(tempPath, wavData);

            var player = new System.Media.SoundPlayer(tempPath);
            player.Play();
        }

        // Добавляет WAV-заголовок к чистым PCM-данным
        private byte[] AddWavHeader(byte[] pcmData, int sampleRate, int channels, int bitsPerSample)
        {
            using var ms = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(ms);

            int byteRate = sampleRate * channels * bitsPerSample / 8;
            int blockAlign = channels * bitsPerSample / 8;

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + pcmData.Length);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)blockAlign);
            writer.Write((short)bitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(pcmData.Length);
            writer.Write(pcmData);

            return ms.ToArray();
        }
    }
}