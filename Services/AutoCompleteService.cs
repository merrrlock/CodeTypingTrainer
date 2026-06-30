using System.Collections.Generic;

namespace CodeTypingTrainer.Services
{
    public class AutoCompleteService
    {
        private readonly LanguageRegistry _registry;

        private static readonly Dictionary<char, char> _universalPairs = new()
        {
            { '(', ')' },
            { '[', ']' },
            { '"', '"' },
            { '\'', '\'' }
        };

        public AutoCompleteService(LanguageRegistry registry)
        {
            _registry = registry;
        }

        // Получить парный символ для текущего языка
        public bool TryGetPair(char input, string language, out char closing)
        {
            var pairs = _registry.Get(language).AutoPairs;
            return pairs.TryGetValue(input, out closing);
        }

        // Подсказки по текущему слову и языку
        public List<string> GetSuggestions(string word, string language)
        {
            if (string.IsNullOrEmpty(word) || word.Length < 2)
                return new List<string>();

            var keywords = _registry.Get(language).Keywords;
            var result = new List<string>();

            foreach (var kw in keywords)
            {
                if (kw.StartsWith(word) && kw != word)
                    result.Add(kw);
                if (result.Count >= 5) break;
            }
            return result;
        }
    }
}