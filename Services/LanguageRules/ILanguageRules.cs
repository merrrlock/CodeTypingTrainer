namespace CodeTypingTrainer.Services.LanguageRules
{
    // Контракт, который обязан реализовать каждый язык
    public interface ILanguageRules
    {
        string Language { get; }
        int IndentSize { get; }
        string IndentChar { get; }          // "space" или "tab"
        bool UseBraceBlocks { get; }        // есть ли блоки через {}

        // Парные символы: открывающий → закрывающий
        Dictionary<char, char> AutoPairs { get; }

        // Ключевые слова для автодополнения
        List<string> Keywords { get; }

        // Нужно ли увеличить отступ после Enter на данной строке?
        bool ShouldIncreaseIndent(string currentLine);

        // Нужно ли уменьшить отступ на новой строке?
        bool ShouldDecreaseIndent(string currentLine);
    }
}