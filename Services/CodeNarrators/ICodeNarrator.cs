using System.Collections.Generic;
using CodeTypingTrainer.Models;

namespace CodeTypingTrainer.Services.CodeNarrators
{
    public interface ICodeNarrator
    {
        string Language { get; }

        // Разбить строку кода на сегменты с фразами для озвучки
        List<NarratedSegment> NarrateLineSegments(string codeLine);

        List<string> SplitIntoLines(string code);
    }
}