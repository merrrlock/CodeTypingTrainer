using System.Collections.Generic;

namespace CodeTypingTrainer.Services.LanguageRules
{
    // Базовый класс для языков с блоками через {}
    // Наследуют: JavaScript, C++, Java, Go
    public abstract class BraceLanguageRules : ILanguageRules
    {
        public abstract string Language { get; }
        public abstract int IndentSize { get; }
        public virtual string IndentChar => "space";
        public bool UseBraceBlocks => true;

        public virtual Dictionary<char, char> AutoPairs => new()
        {
            { '(', ')' },
            { '[', ']' },
            { '{', '}' },
            { '"', '"' },
            { '\'', '\'' }
        };

        public abstract List<string> Keywords { get; }

        // Увеличиваем отступ если строка заканчивается на {
        public virtual bool ShouldIncreaseIndent(string currentLine)
            => currentLine.TrimEnd().EndsWith("{");

        // Уменьшаем отступ если новая строка начинается с }
        public virtual bool ShouldDecreaseIndent(string currentLine)
            => currentLine.TrimStart().StartsWith("}");
    }
}