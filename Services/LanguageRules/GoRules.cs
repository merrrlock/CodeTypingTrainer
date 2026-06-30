using System.Collections.Generic;

namespace CodeTypingTrainer.Services.LanguageRules
{
    public class GoRules : BraceLanguageRules
    {
        public override string Language => "Go";
        public override int IndentSize => 1;
        public override string IndentChar => "tab";  // gofmt стандарт

        public override List<string> Keywords => new()
        {
            "func", "package", "import", "var", "const", "type",
            "struct", "interface", "if", "else", "for", "range",
            "return", "go", "chan", "select", "case", "default",
            "defer", "map", "true", "false", "nil", "error",
            "break", "continue", "goto", "fallthrough", "switch",
            "make", "new", "len", "cap", "append", "copy", "close"
        };
    }
}