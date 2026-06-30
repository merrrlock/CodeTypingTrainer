using System.Collections.Generic;

namespace CodeTypingTrainer.Services.CodeNarrators
{
    // Хранит нарраторов для всех языков, выдаёт нужный по имени
    public class NarratorRegistry
    {
        private readonly Dictionary<string, ICodeNarrator> _narrators = new();

        public NarratorRegistry()
        {
            Register(new PythonNarrator());
            // JavaScript, C++, Java, Go подключим позже
        }

        private void Register(ICodeNarrator narrator)
            => _narrators[narrator.Language] = narrator;

        public ICodeNarrator Get(string language)
            => _narrators.TryGetValue(language, out var n) ? n : null;

        public bool IsSupported(string language)
            => _narrators.ContainsKey(language);
    }
}