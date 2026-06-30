using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodeTypingTrainer.Services
{
    public class OnScreenKeyboardService
    {
        private readonly KeyboardLayoutService _layout = new();
        private readonly Dictionary<char, Border> _keyVisuals = new();
        private readonly Dictionary<char, Color> _keyBaseColors = new();
        private readonly Dictionary<SpecialKeyType, Border> _specialVisuals = new();
        private readonly Dictionary<SpecialKeyType, Color> _specialBaseColors = new();

        // Какие символы требуют Shift (верхний ряд клавиши)
        private readonly HashSet<char> _shiftChars = new();

        private static Color Dim(Color c) => Color.FromRgb(
            (byte)(c.R * 0.30), (byte)(c.G * 0.30), (byte)(c.B * 0.30));

        private static readonly Color PressColor = Color.FromRgb(0xCD, 0xD6, 0xF4);
        private static readonly Color HintColor = Color.FromRgb(0x89, 0xB4, 0xFA);

        public void BuildKeyboard(Canvas canvas)
        {
            canvas.Children.Clear();
            _keyVisuals.Clear();
            _keyBaseColors.Clear();
            _specialVisuals.Clear();
            _specialBaseColors.Clear();
            _shiftChars.Clear();

            var rows = _layout.GetRows();
            double keySize = 50;
            double gap = 5;
            double canvasWidth = 900;

            // Считаем ширину каждого ряда для центрирования
            var rowWidths = new List<double>();
            foreach (var row in rows)
            {
                double w = 0;
                foreach (var key in row) w += keySize * key.WidthUnits + gap;
                rowWidths.Add(w - gap);
            }
            double maxWidth = rowWidths.Max();

            double y = 0;
            for (int r = 0; r < rows.Count; r++)
            {
                double startX = (canvasWidth - maxWidth) / 2.0 + (maxWidth - rowWidths[r]) / 2.0;
                double x = startX;

                foreach (var key in rows[r])
                {
                    double width = keySize * key.WidthUnits;
                    DrawKey(canvas, key, x, y, width, keySize);
                    x += width + gap;

                    // Запоминаем какие символы требуют Shift
                    if (!key.IsSpecial && key.IsSplit)
                        _shiftChars.Add(key.TopChar!.Value);
                }
                y += keySize + gap;
            }
        }

        private void DrawKey(Canvas canvas, KeyDefinition key, double x, double y, double width, double height)
        {
            var fingerColor = KeyboardLayoutService.FingerColors[key.Finger];
            var dimColor = Dim(fingerColor);

            var border = new Border
            {
                Width = width,
                Height = height,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(dimColor),
                ClipToBounds = true
            };
            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);

            if (key.IsSpecial)
            {
                var text = new TextBlock
                {
                    Text = key.Label,
                    Foreground = new SolidColorBrush(Colors.White),
                    FontWeight = FontWeights.Bold,
                    FontSize = key.Label.Length > 4 ? 12 : 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.75
                };
                border.Child = text;

                _specialVisuals[key.SpecialType] = border;
                _specialBaseColors[key.SpecialType] = dimColor;
            }
            else if (key.IsSplit)
            {
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition());
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) });
                grid.RowDefinitions.Add(new RowDefinition());

                var topCell = BuildHalfCell(key.TopChar!.Value, true, dimColor);
                var botCell = BuildHalfCell(key.BottomChar!.Value, false, dimColor);

                Grid.SetRow(topCell, 0);
                Grid.SetRow(botCell, 2);
                grid.Children.Add(topCell);
                grid.Children.Add(botCell);

                border.Child = grid;
                _keyVisuals[key.TopChar.Value] = topCell;
                _keyVisuals[key.BottomChar.Value] = botCell;
                _keyBaseColors[key.TopChar.Value] = dimColor;
                _keyBaseColors[key.BottomChar.Value] = dimColor;
            }
            else
            {
                char c = key.BottomChar ?? ' ';
                border.Child = new TextBlock
                {
                    Text = c.ToString().ToUpper(),
                    Foreground = new SolidColorBrush(Colors.White),
                    FontWeight = FontWeights.Bold,
                    FontSize = 17,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                _keyVisuals[c] = border;
                _keyBaseColors[c] = dimColor;
            }

            canvas.Children.Add(border);
        }

        private Border BuildHalfCell(char c, bool isTop, Color bgColor)
        {
            var half = new Border
            {
                Background = new SolidColorBrush(bgColor),
                CornerRadius = isTop ? new CornerRadius(4, 4, 0, 0) : new CornerRadius(0, 0, 4, 4)
            };
            half.Child = new TextBlock
            {
                Text = char.IsLetterOrDigit(c) ? c.ToString().ToUpper() : c.ToString(),
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            return half;
        }

        // -------------------------------------------------------
        // НАЖАТИЕ — кратковременная вспышка
        // -------------------------------------------------------
        public void HighlightKey(char c)
        {
            var v = GetCharVisual(c);
            if (v != null) v.Background = new SolidColorBrush(PressColor);
        }

        public void ResetKey(char c)
        {
            var v = GetCharVisual(c);
            if (v == null) return;
            char key = char.ToLower(c);
            var baseColor = _keyBaseColors.TryGetValue(key, out var bc) ? bc : Color.FromRgb(0x31, 0x32, 0x44);
            v.Background = new SolidColorBrush(baseColor);
        }

        public void HighlightSpecial(SpecialKeyType type)
        {
            if (_specialVisuals.TryGetValue(type, out var v))
                v.Background = new SolidColorBrush(PressColor);
        }

        public void ResetSpecial(SpecialKeyType type)
        {
            if (_specialVisuals.TryGetValue(type, out var v))
            {
                var baseColor = _specialBaseColors.TryGetValue(type, out var bc) ? bc : Color.FromRgb(0x31, 0x32, 0x44);
                v.Background = new SolidColorBrush(baseColor);
            }
        }

        // -------------------------------------------------------
        // ПОДСКАЗКА — может подсвечивать несколько клавиш разом
        // (например Shift + 9 для открывающей скобки)
        // -------------------------------------------------------
        private readonly List<Border> _hintVisuals = new();
        private readonly List<char> _hintChars = new();
        private SpecialKeyType? _hintSpecial;

        public void ShowHintForChar(char expected)
        {
            ClearHint();

            bool needsShift = _shiftChars.Contains(expected) && !char.IsLetter(expected);
            // Заглавные буквы тоже требуют Shift
            if (char.IsUpper(expected)) needsShift = true;

            char lookupChar = char.IsLetter(expected) ? char.ToLower(expected) : expected;

            var mainVisual = GetCharVisual(lookupChar);
            if (mainVisual != null)
            {
                mainVisual.Background = new SolidColorBrush(HintColor);
                _hintVisuals.Add(mainVisual);
                _hintChars.Add(lookupChar);
            }

            if (needsShift && _specialVisuals.TryGetValue(SpecialKeyType.LeftShift, out var shiftVisual))
            {
                shiftVisual.Background = new SolidColorBrush(HintColor);
                _hintVisuals.Add(shiftVisual);
                _hintSpecial = SpecialKeyType.LeftShift;
            }
        }

        public void ShowHintForSpecial(SpecialKeyType type)
        {
            ClearHint();
            if (_specialVisuals.TryGetValue(type, out var v))
            {
                v.Background = new SolidColorBrush(HintColor);
                _hintVisuals.Add(v);
                _hintSpecial = type;
            }
        }

        public void ClearHint()
        {
            foreach (var c in _hintChars) ResetKey(c);
            if (_hintSpecial.HasValue) ResetSpecial(_hintSpecial.Value);

            _hintVisuals.Clear();
            _hintChars.Clear();
            _hintSpecial = null;
        }

        private Border GetCharVisual(char c)
        {
            if (c == ' ') return _specialVisuals.TryGetValue(SpecialKeyType.Space, out var s) ? s : null;
            char lower = char.ToLower(c);
            return _keyVisuals.TryGetValue(lower, out var v) ? v : null;
        }
    }
}