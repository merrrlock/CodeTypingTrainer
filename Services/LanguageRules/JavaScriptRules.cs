using System.Collections.Generic;

namespace CodeTypingTrainer.Services.LanguageRules
{
    public class JavaScriptRules : BraceLanguageRules
    {
        public override string Language => "JavaScript";
        public override int IndentSize => 2;

        // Добавляем backtick к стандартным парам
        public override Dictionary<char, char> AutoPairs
        {
            get
            {
                var pairs = base.AutoPairs;
                pairs['`'] = '`';
                return pairs;
            }
        }

        public override List<string> Keywords => new()
        {
            "function", "const", "let", "var", "if", "else", "for",
            "while", "return", "class", "new", "this", "import",
            "export", "default", "async", "await", "try", "catch",
            "finally", "typeof", "instanceof", "true", "false",
            "null", "undefined", "switch", "case", "break", "continue",
            "do", "in", "of", "from", "extends", "super", "static",
            "get", "set", "delete", "throw", "void", "yield"
        };
    }
}