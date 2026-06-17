using CodeTypingTrainer.Services.LanguageRules;

namespace CodeTypingTrainer.Services
{
    // Хранит все доступные языки и выдаёт нужный по имени
    public class LanguageRegistry
    {
        private readonly Dictionary<string, ILanguageRules> _rules = new();

        public LanguageRegistry()
        {
            Register(new PythonRules());
            Register(new JavaScriptRules());
            Register(new CppRules());
            Register(new JavaRules());
            Register(new GoRules());
        }

        private void Register(ILanguageRules rules)
            => _rules[rules.Language] = rules;

        // Получить правила по имени языка
        // Если язык не найден — возвращаем Python как безопасный дефолт
        public ILanguageRules Get(string language)
            => _rules.TryGetValue(language, out var rules) ? rules : new PythonRules();

        public IEnumerable<string> AvailableLanguages => _rules.Keys;
    }
}