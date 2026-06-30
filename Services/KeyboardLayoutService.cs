using System.Collections.Generic;
using System.Windows.Media;

namespace CodeTypingTrainer.Services
{

    public enum SpecialKeyType
    {
        None, Backspace, Space, Tab, CapsLock, Enter, LeftShift, RightShift, LeftCtrl, RightCtrl
    }

    public enum FingerZone
    {
        LeftPinky, LeftRing, LeftMiddle, LeftIndex,
        Thumb,
        RightIndex, RightMiddle, RightRing, RightPinky
    }

    public class KeyDefinition
    {
        public SpecialKeyType SpecialType { get; set; } = SpecialKeyType.None;
        public char? BottomChar { get; set; }
        public char? TopChar { get; set; }
        public double WidthUnits { get; set; } = 1.0;
        public FingerZone Finger { get; set; }
        public string Label { get; set; }      // для служебных клавиш (Space, Backspace)
        public bool IsSpecial { get; set; }    // служебная клавиша — не имеет символа

        public bool IsSplit => TopChar.HasValue && BottomChar.HasValue
                            && TopChar != BottomChar && !IsSpecial;
    }

    public class KeyboardLayoutService
    {
        // Цвета по зонам пальцев — схема с фото
        public static readonly Dictionary<FingerZone, Color> FingerColors = new()
        {
            { FingerZone.LeftPinky,   Color.FromRgb(0xCA, 0x9E, 0xE8) }, // фиолетовый
            { FingerZone.LeftRing,    Color.FromRgb(0x8C, 0xAA, 0xEE) }, // синий
            { FingerZone.LeftMiddle,  Color.FromRgb(0x7E, 0xC8, 0x7E) }, // зелёный
            { FingerZone.LeftIndex,   Color.FromRgb(0xF0, 0xA8, 0x50) }, // оранжевый
            { FingerZone.Thumb,       Color.FromRgb(0x90, 0x90, 0xA8) }, // серый
            { FingerZone.RightIndex,  Color.FromRgb(0xE8, 0xD0, 0x50) }, // жёлтый
            { FingerZone.RightMiddle, Color.FromRgb(0x7E, 0xC8, 0x7E) }, // зелёный
            { FingerZone.RightRing,   Color.FromRgb(0x8C, 0xAA, 0xEE) }, // синий
            { FingerZone.RightPinky,  Color.FromRgb(0xCA, 0x9E, 0xE8) }, // фиолетовый
        };
        public List<List<KeyDefinition>> GetRows()
        {
            return new List<List<KeyDefinition>>
            {
                // Цифровой ряд
                new()
                {
                    Key('`','~', FingerZone.LeftPinky),
                    Key('1','!', FingerZone.LeftPinky),
                    Key('2','@', FingerZone.LeftRing),
                    Key('3','#', FingerZone.LeftMiddle),
                    Key('4','$', FingerZone.LeftIndex),
                    Key('5','%', FingerZone.LeftIndex),
                    Key('6','^', FingerZone.RightIndex),
                    Key('7','&', FingerZone.RightIndex),
                    Key('8','*', FingerZone.RightMiddle),
                    Key('9','(', FingerZone.RightRing),
                    Key('0',')', FingerZone.RightPinky),
                    Key('-','_', FingerZone.RightPinky),
                    Key('=','+', FingerZone.RightPinky),
                    Special("⌫", FingerZone.RightPinky, SpecialKeyType.Backspace, width: 1.6),
                },
                // QWERTY ряд
                new()
                {
                    Special("Tab", FingerZone.LeftPinky, SpecialKeyType.Tab, width: 1.4),
                    Key('q', FingerZone.LeftPinky),
                    Key('w', FingerZone.LeftRing),
                    Key('e', FingerZone.LeftMiddle),
                    Key('r', FingerZone.LeftIndex),
                    Key('t', FingerZone.LeftIndex),
                    Key('y', FingerZone.RightIndex),
                    Key('u', FingerZone.RightIndex),
                    Key('i', FingerZone.RightMiddle),
                    Key('o', FingerZone.RightRing),
                    Key('p', FingerZone.RightPinky),
                    Key('[','{', FingerZone.RightPinky),
                    Key(']','}', FingerZone.RightPinky),
                    Key('\\','|', FingerZone.RightPinky),
                },
                // ASDF ряд
                new()
                {
                    Special("Caps", FingerZone.LeftPinky, SpecialKeyType.CapsLock, width: 1.6),
                    Key('a', FingerZone.LeftPinky),
                    Key('s', FingerZone.LeftRing),
                    Key('d', FingerZone.LeftMiddle),
                    Key('f', FingerZone.LeftIndex),
                    Key('g', FingerZone.LeftIndex),
                    Key('h', FingerZone.RightIndex),
                    Key('j', FingerZone.RightIndex),
                    Key('k', FingerZone.RightMiddle),
                    Key('l', FingerZone.RightRing),
                    Key(';',':', FingerZone.RightPinky),
                    Key('\'','"', FingerZone.RightPinky),
                    Special("Enter", FingerZone.RightPinky, SpecialKeyType.Enter, width: 1.8),
                },
                // ZXCV ряд
                new()
                {
                    Special("Shift", FingerZone.LeftPinky, SpecialKeyType.LeftShift, width: 2.0),
                    Key('z', FingerZone.LeftPinky),
                    Key('x', FingerZone.LeftRing),
                    Key('c', FingerZone.LeftMiddle),
                    Key('v', FingerZone.LeftIndex),
                    Key('b', FingerZone.LeftIndex),
                    Key('n', FingerZone.RightIndex),
                    Key('m', FingerZone.RightIndex),
                    Key(',','<', FingerZone.RightMiddle),
                    Key('.','>', FingerZone.RightRing),
                    Key('/','?', FingerZone.RightPinky),
                    Special("Shift", FingerZone.RightPinky, SpecialKeyType.RightShift, width: 2.0),
                },
                // Нижний ряд — Ctrl + Space + Ctrl
                new()
                {
                    Special("Ctrl", FingerZone.LeftPinky, SpecialKeyType.LeftCtrl, width: 1.6),
                    Special("Space", FingerZone.Thumb, SpecialKeyType.Space, width: 6.0),
                    Special("Ctrl", FingerZone.RightPinky, SpecialKeyType.RightCtrl, width: 1.6),
                },
            };
        }

        private KeyDefinition Key(char letter, FingerZone finger)
            => new() { BottomChar = letter, TopChar = letter, Finger = finger };

        private KeyDefinition Key(char bottom, char top, FingerZone finger)
            => new() { BottomChar = bottom, TopChar = top, Finger = finger };

        private KeyDefinition Special(string label, FingerZone finger, SpecialKeyType type, double width = 1.0)
            => new() { Label = label, IsSpecial = true, Finger = finger, WidthUnits = width, SpecialType = type };
    }
}