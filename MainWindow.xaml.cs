using CodeTypingTrainer.Models;
using CodeTypingTrainer.Services;
using CodeTypingTrainer.Services.CodeNarrators;
using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CodeTypingTrainer
{
    public partial class MainWindow : Window
    {
        private readonly StatsAggregationService _statsAggregator = new();
        private readonly KeyboardLayoutService _keyboardLayout = new();
        private bool _sidePanelCollapsed = false;

        private Services.SpecialKeyType? _lastHighlightedSpecial;





        private readonly OnScreenKeyboardService _onScreenKeyboard = new();
        private readonly DispatcherTimer _keyReleaseTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
        private char? _lastHighlightedKey;

        private readonly StorageService _storage = new();
        private UserProfile _profile;

        private readonly ConfigService _config = new();
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

            _onScreenKeyboard.BuildKeyboard(OnScreenKeyboardCanvas);
            _keyReleaseTimer.Tick += (s, e) =>
            {
                if (_lastHighlightedKey.HasValue)
                {
                    _onScreenKeyboard.ResetKey(_lastHighlightedKey.Value);
                    _lastHighlightedKey = null;
                }
                if (_lastHighlightedSpecial.HasValue)
                {
                    _onScreenKeyboard.ResetSpecial(_lastHighlightedSpecial.Value);
                    _lastHighlightedSpecial = null;
                }
                _keyReleaseTimer.Stop();
            };

            _autoComplete = new AutoCompleteService(_languageRegistry);

            _profile = _storage.LoadProfile();

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
                return;
            }

            var snippet = _library.GetRandom(_currentLanguage);
            if (snippet == null)
            {
                return;
            }

            _session = new TypingSession
            {
                Snippet = snippet,
                StartTime = DateTime.Now,
                IsActive = true,
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
            var (audioData, error) = await _googleTts.SynthesizeAsync(phrase);

            byte[] wavData = AddWavHeader(audioData, 24000, 1, 16);
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tts_segment.wav");
            System.IO.File.WriteAllBytes(tempPath, wavData);

            var player = new System.Media.SoundPlayer(tempPath);
            player.Play();

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

                            _userInput = input.Substring(0, Math.Min(caret, input.Length));

                            _session.CurrentInput = _userInput;
                            _session.CaretPosition = _userInput.Length;
                            _session.TotalKeystrokes = _userInput.Length;

                            string target = _session.Snippet.Code;

                            int errors = 0;
                            for (int i = 0; i < _userInput.Length && i < target.Length; i++)
                                if (_userInput[i] != target[i]) errors++;

                            for (int i = 0; i < _userInput.Length && i < target.Length; i++)
                            {
                                if (_userInput[i] != target[i])
                                    _session.RegisterErrorAtPosition(i, target[i]);
                            }

                            AccuracyText.Text = $"{_session.Accuracy}%";
                            _highlight.UpdateHighlight(CodeDisplay, target, _userInput, caret);
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


            e.Handled = true;


            if (_session != null)
            {
                string input = InputBox.Text;
                int caret = InputBox.CaretIndex;

                _userInput = input.Substring(0, Math.Min(caret, input.Length));

                _session.CurrentInput = _userInput;
                _session.CaretPosition = _userInput.Length;
                _session.TotalKeystrokes = _userInput.Length;

                string target = _session.Snippet.Code;

                int errors = 0;
                for (int i = 0; i < _userInput.Length && i < target.Length; i++)
                    if (_userInput[i] != target[i]) errors++;

                for (int i = 0; i < _userInput.Length && i < target.Length; i++)
                {
                    if (_userInput[i] != target[i])
                        _session.RegisterErrorAtPosition(i, target[i]);
                }

                AccuracyText.Text = $"{_session.Accuracy}%";
                _highlight.UpdateHighlight(CodeDisplay, target, _userInput, _userInput.Length);
                ScrollToCurrentPosition(_userInput.Length);
            }

            UpdateKeyboardHint();
        }

        // -------------------------------------------------------
        // УМНЫЙ BACKSPACE
        // -------------------------------------------------------
        private void HandleBackspaceKey(KeyEventArgs e)
        {
            _onScreenKeyboard.HighlightSpecial(Services.SpecialKeyType.Backspace);
            _lastHighlightedSpecial = Services.SpecialKeyType.Backspace;
            _keyReleaseTimer.Stop();
            _keyReleaseTimer.Start();

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
                _session.TotalKeystrokes = _userInput.Length;

                int errors = 0;
                for (int i = 0; i < _userInput.Length && i < target.Length; i++)
                    if (_userInput[i] != target[i]) errors++;

                for (int i = 0; i < _userInput.Length && i < target.Length; i++)
                {
                    if (_userInput[i] != target[i])
                        _session.RegisterErrorAtPosition(i, target[i]);
                }

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

            _userInput = input.Substring(0, Math.Min(caret, input.Length));

            _session.CurrentInput = _userInput;
            _session.CaretPosition = _userInput.Length;
            _session.TotalKeystrokes = _userInput.Length;

            // Считаем накопительные ошибки и регистрируем их в CharErrors одновременно
            // Проверяем КАЖДУЮ позицию текущего ввода — если на ней неверный символ,
            // регистрируем ошибку на этой позиции (один раз навсегда, независимо от исправлений)
            for (int i = 0; i < _userInput.Length && i < target.Length; i++)
            {
                if (_userInput[i] != target[i])
                    _session.RegisterErrorAtPosition(i, target[i]);
            }

            AccuracyText.Text = $"{_session.Accuracy}%";

            _highlight.UpdateHighlight(CodeDisplay, target, _userInput, _userInput.Length);
            ScrollToCurrentPosition(_userInput.Length);

            // Показываем подсказку — следующая клавиша или Backspace если есть ошибка
            UpdateKeyboardHint();

            if (_suggestionsEnabled)
                UpdateSuggestions(input);

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
            _onScreenKeyboard.ClearHint();
            _timer.Stop();

            // Считаем финальные метрики ДО того как IsActive станет false —
            // иначе WPM всегда будет вычисляться как 0
            int finalWpm = _session.WPM;
            double finalAccuracy = _session.Accuracy;

            _session.IsActive = false;
            InputBox.IsEnabled = false;
            PauseButton.IsEnabled = false;
            SuggestionsPopup.IsOpen = false;
            _speech.StopAll();

            if (_isAudioMode)
                ShowCodeButton.IsEnabled = true;

            _highlight.SetCompleted(CodeDisplay, _session.Snippet.Code);

            TimeSpan elapsed = DateTime.Now - _session.StartTime;
            StartButton.IsEnabled = true;

            SaveCompletedSession(elapsed, finalWpm, finalAccuracy);
        }

        // Сохранение завершённой сессии на диск
        private void SaveCompletedSession(TimeSpan elapsed, int finalWpm, double finalAccuracy)
        {
            // Считаем реальное количество ошибок для этой сессии независимо от режима точности —
            // сравниваем финальный ввод с эталоном посимвольно
            int actualErrorCount = CountActualErrors();

            var record = new SessionRecord
            {
                Timestamp = DateTime.Now,
                Language = _session.Snippet.Language,
                SnippetTitle = _session.Snippet.Title,
                Difficulty = _session.Snippet.Difficulty,
                WPM = finalWpm,
                Accuracy = finalAccuracy,
                DurationSeconds = (int)elapsed.TotalSeconds,
                ErrorCount = actualErrorCount,
                Mode = _isAudioMode ? "Audio" : "Normal"
            };

            _storage.SaveSessionRecord(record);

            _profile.ApplySession(record, _session.TotalKeystrokes, _session.CharErrors);
            _storage.SaveProfile(_profile);
        }

        // Считает реальное количество ошибок независимо от режима точности —
        // используется только для статистики, не влияет на сам Accuracy
        private int CountActualErrors()
        {
            return _session.CumulativeErrors;
        }

        // Сохранение завершённой сессии на диск
        private void SaveCompletedSession(TimeSpan elapsed)
        {
            var record = new SessionRecord
            {
                Timestamp = DateTime.Now,
                Language = _session.Snippet.Language,
                SnippetTitle = _session.Snippet.Title,
                Difficulty = _session.Snippet.Difficulty,
                WPM = _session.WPM,
                Accuracy = _session.Accuracy,
                DurationSeconds = (int)elapsed.TotalSeconds,
                ErrorCount = _session.CumulativeErrors,
                Mode = _isAudioMode ? "Audio" : "Normal"
            };

            _storage.SaveSessionRecord(record);

            _profile.ApplySession(record, _session.TotalKeystrokes, _session.CharErrors);
            _storage.SaveProfile(_profile);
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Сохраняем профиль на случай если он не был сохранён после последней сессии
            _storage.SaveProfile(_profile);
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
            }
            else
            {
                _session.IsPaused = false;
                _timer.Start();
                InputBox.IsEnabled = true;
                InputBox.Focus();
                PauseButton.Content = "⏸ Пауза";
            }
        }

        // -------------------------------------------------------
        // ПЕРЕЗАПУСК
        // -------------------------------------------------------
        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            _onScreenKeyboard.ClearHint();
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
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: переключение темы
        }

        private async void TestTts_Click(object sender, RoutedEventArgs e)
        {

            var (audioData, error) = await _googleTts.SynthesizeAsync("Привет, это тест синтеза речи");

            if (error != null)
            {
                MessageBox.Show(error, "Ошибка TTS", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }


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


        private void InputBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;

            char typedChar = e.Text[0];

            // Снимаем подсветку с предыдущей клавиши немедленно
            if (_lastHighlightedKey.HasValue)
                _onScreenKeyboard.ResetKey(_lastHighlightedKey.Value);

            _onScreenKeyboard.HighlightKey(typedChar);
            _lastHighlightedKey = typedChar;

            _keyReleaseTimer.Stop();
            _keyReleaseTimer.Start();
        }

        // -------------------------------------------------------
        // НАВИГАЦИЯ ПО ВКЛАДКАМ
        // -------------------------------------------------------
        private void TabTraining_Click(object sender, RoutedEventArgs e)
        {
            TrainingTab.Visibility = Visibility.Visible;
            StatsTab.Visibility = Visibility.Collapsed;
            SetActiveTab(TabTrainingButton);
        }

        private void TabStats_Click(object sender, RoutedEventArgs e)
        {
            TrainingTab.Visibility = Visibility.Collapsed;
            StatsTab.Visibility = Visibility.Visible;
            SetActiveTab(TabStatsButton);
            LoadStatsTab();
        }

        private void SetActiveTab(Button active)
        {
            TabTrainingButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
            TabTrainingButton.Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86));
            TabStatsButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
            TabStatsButton.Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86));

            active.BorderBrush = new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA));
            active.Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4));
        }

        // -------------------------------------------------------
        // СВОРАЧИВАНИЕ БОКОВОЙ ПАНЕЛИ
        // -------------------------------------------------------
        private void CollapsePanel_Click(object sender, RoutedEventArgs e)
        {
            _sidePanelCollapsed = !_sidePanelCollapsed;

            if (_sidePanelCollapsed)
            {
                SidePanelColumn.Width = new GridLength(40);
                SidePanelContent.Visibility = Visibility.Collapsed;
                SideModeLabel.Visibility = Visibility.Collapsed;
                SideLangLabel.Visibility = Visibility.Collapsed;
                CollapseButton.Content = "▶";
            }
            else
            {
                SidePanelColumn.Width = new GridLength(180);
                SidePanelContent.Visibility = Visibility.Visible;
                SideModeLabel.Visibility = Visibility.Visible;
                SideLangLabel.Visibility = Visibility.Visible;
                CollapseButton.Content = "≡";
            }
        }

        // -------------------------------------------------------
        // ВКЛАДКА СТАТИСТИКИ
        // -------------------------------------------------------
        private void LoadStatsTab()
        {
            var profile = _storage.LoadProfile();
            var sessions = _storage.LoadAllSessions();

            // Сводка
            TotalSessionsText.Text = profile.TotalSessions.ToString();
            BestWpmText.Text = profile.BestWPM.ToString("0");
            AvgAccuracyText.Text = $"{profile.AverageAccuracy:0.0}%";
            TotalKeystrokesText.Text = profile.TotalKeystrokes.ToString();

            // График
            var points = _statsAggregator.BuildProgressPoints(sessions);
            ProgressChartCanvas.Loaded += (s, e) => DrawProgressChart(points);
            if (ProgressChartCanvas.ActualWidth > 0)
                DrawProgressChart(points);

            // Тепловая карта
            var intensity = _statsAggregator.NormalizeErrorIntensity(profile.CharErrors);
            DrawHeatmapKeyboard(intensity);
        }

        private void DrawProgressChart(List<StatsAggregationService.ProgressPoint> points)
        {
            ProgressChartCanvas.Children.Clear();
            if (points.Count == 0)
            {
                var t = new TextBlock
                {
                    Text = "Нет данных — завершите хотя бы одну тренировку.",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86)),
                    FontSize = 13
                };
                Canvas.SetLeft(t, 12); Canvas.SetTop(t, 80);
                ProgressChartCanvas.Children.Add(t);
                return;
            }

            double w = ProgressChartCanvas.ActualWidth > 0 ? ProgressChartCanvas.ActualWidth : 860;
            double h = ProgressChartCanvas.ActualHeight > 0 ? ProgressChartCanvas.ActualHeight : 200;
            double pad = 28;
            double maxWpm = System.Math.Max(points.Max(p => p.WPM), 10);
            double stepX = points.Count > 1 ? (w - 2 * pad) / (points.Count - 1) : 0;

            var wpmPts = new PointCollection();
            var accPts = new PointCollection();

            for (int i = 0; i < points.Count; i++)
            {
                double x = pad + i * stepX;
                wpmPts.Add(new Point(x, h - pad - (points[i].WPM / maxWpm) * (h - 2 * pad)));
                accPts.Add(new Point(x, h - pad - (points[i].Accuracy / 100.0) * (h - 2 * pad)));
            }

            ProgressChartCanvas.Children.Add(new Polyline
            {
                Points = wpmPts,
                Stroke = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1)),
                StrokeThickness = 2
            });
            ProgressChartCanvas.Children.Add(new Polyline
            {
                Points = accPts,
                Stroke = new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)),
                StrokeThickness = 2
            });

            foreach (var p in wpmPts) AddChartDot(p, Color.FromRgb(0xA6, 0xE3, 0xA1));
            foreach (var p in accPts) AddChartDot(p, Color.FromRgb(0x89, 0xB4, 0xFA));
        }

        private void AddChartDot(Point p, Color c)
        {
            var dot = new Ellipse { Width = 6, Height = 6, Fill = new SolidColorBrush(c) };
            Canvas.SetLeft(dot, p.X - 3); Canvas.SetTop(dot, p.Y - 3);
            ProgressChartCanvas.Children.Add(dot);
        }

        private void DrawHeatmapKeyboard(Dictionary<char, double> intensity)
        {
            // Переиспользуем тот же подход что в StatsWindow — но рисуем на KeyboardCanvas
            KeyboardCanvas.Children.Clear();
            var rows = _keyboardLayout.GetRows();
            double keySize = 56, gap = 6, y = 0;

            foreach (var row in rows)
            {
                double x = 0;
                if (row == rows[1]) x = keySize * 0.4;
                else if (row == rows[2]) x = keySize * 0.7;
                else if (row == rows[3]) x = keySize * 1.0;

                foreach (var key in row)
                {
                    DrawHeatmapKey(key, x, y, keySize, keySize, intensity);
                    x += keySize + gap;
                }
                y += keySize + gap;
            }
        }

        private void DrawHeatmapKey(KeyDefinition key, double x, double y,
            double width, double height, Dictionary<char, double> intensity)
        {
            Color Interp(double level)
            {
                var b = Color.FromRgb(0x31, 0x32, 0x44);
                var e = Color.FromRgb(0xF3, 0x8B, 0xA8);
                return Color.FromRgb(
                    (byte)(b.R + (e.R - b.R) * level),
                    (byte)(b.G + (e.G - b.G) * level),
                    (byte)(b.B + (e.B - b.B) * level));
            }

            var border = new Border
            {
                Width = width,
                Height = height,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)),
                ClipToBounds = true
            };
            Canvas.SetLeft(border, x); Canvas.SetTop(border, y);

            if (key.IsSplit)
            {
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition());
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) });
                grid.RowDefinitions.Add(new RowDefinition());

                Border Half(char c, bool top)
                {
                    double lv = intensity.TryGetValue(c, out var v) ? v : 0;
                    var h = new Border
                    {
                        Background = new SolidColorBrush(Interp(lv)),
                        CornerRadius = top ? new CornerRadius(6, 6, 0, 0) : new CornerRadius(0, 0, 6, 6)
                    };
                    h.Child = new TextBlock
                    {
                        Text = char.IsLetterOrDigit(c) ? c.ToString().ToUpper() : c.ToString(),
                        Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)),
                        FontWeight = FontWeights.Bold,
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    return h;
                }

                var top = Half(key.TopChar.Value, true);
                var bot = Half(key.BottomChar.Value, false);
                Grid.SetRow(top, 0); Grid.SetRow(bot, 2);
                grid.Children.Add(top); grid.Children.Add(bot);
                border.Child = grid;
            }
            else
            {
                char c = key.BottomChar ?? ' ';
                double lv = intensity.TryGetValue(c, out var v) ? v : 0;
                border.Background = new SolidColorBrush(Interp(lv));
                border.Child = new TextBlock
                {
                    Text = c.ToString().ToUpper(),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)),
                    FontWeight = FontWeights.Bold,
                    FontSize = 17,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            KeyboardCanvas.Children.Add(border);
        }

        private void ClearStatsButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Удалить всю статистику?\n\nИсторию сессий, рекорды и тепловую карту нельзя восстановить.",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes) return;

            _storage.ClearAllData();
            _profile = _storage.LoadProfile();
            LoadStatsTab();
        }

        private void UpdateKeyboardHint()
        {
            if (_session == null || !_session.IsActive) return;

            string target = _session.Snippet.Code;

            bool hasError = _userInput.Length > 0
                && _userInput.Length <= target.Length
                && _userInput[_userInput.Length - 1] != target[_userInput.Length - 1];

            if (hasError)
            {
                _onScreenKeyboard.ShowHintForSpecial(Services.SpecialKeyType.Backspace);
                return;
            }

            int nextPos = _userInput.Length;
            if (nextPos >= target.Length)
            {
                _onScreenKeyboard.ClearHint();
                return;
            }

            char next = target[nextPos];

            if (next == '\n')
            {
                _onScreenKeyboard.ShowHintForSpecial(Services.SpecialKeyType.Enter);
            }
            else if (next == '\t')
            {
                _onScreenKeyboard.ShowHintForSpecial(Services.SpecialKeyType.Tab);
            }
            else
            {
                _onScreenKeyboard.ShowHintForChar(next);
            }
        }

        // Фильтры периода
        private void FilterAll_Click(object sender, RoutedEventArgs e) => LoadStatsTab();
        private void FilterWeek_Click(object sender, RoutedEventArgs e) => LoadStatsTab();
        private void FilterToday_Click(object sender, RoutedEventArgs e) => LoadStatsTab();

        private void ThemeLight_Click(object sender, RoutedEventArgs e) { }
        private void ThemeDark_Click(object sender, RoutedEventArgs e) { }
    }
}