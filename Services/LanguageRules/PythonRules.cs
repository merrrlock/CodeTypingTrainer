using System.Collections.Generic;

namespace CodeTypingTrainer.Services.LanguageRules
{
    public class PythonRules : ILanguageRules
    {
        public string Language => "Python";
        public int IndentSize => 4;
        public string IndentChar => "space";
        public bool UseBraceBlocks => false;

        public Dictionary<char, char> AutoPairs => new()
        {
            { '(', ')' },
            { '[', ']' },
            { '"', '"' },
            { '\'', '\'' }
            // { } намеренно отсутствует — в Python блоки через отступы
        };

        public List<string> Keywords => new()
        {
            "def", "class", "if", "elif", "else", "for", "while",
            "return", "import", "from", "in", "not", "and", "or",
            "try", "except", "finally", "with", "as", "lambda",
            "True", "False", "None", "pass", "break", "continue",
            "raise", "yield", "global", "nonlocal", "assert", "del"
        };

        // После двоеточия — увеличиваем отступ
        public bool ShouldIncreaseIndent(string currentLine)
            => currentLine.TrimEnd().EndsWith(":");

        // После return/break/pass/continue — уменьшаем отступ
        private static readonly string[] _dedentKeywords =
            { "return", "break", "pass", "continue", "raise" };

        public bool ShouldDecreaseIndent(string currentLine)
        {
            string trimmed = currentLine.Trim();
            foreach (var kw in _dedentKeywords)
                if (trimmed == kw || trimmed.StartsWith(kw + " "))
                    return true;
            return false;
        }
    }
}