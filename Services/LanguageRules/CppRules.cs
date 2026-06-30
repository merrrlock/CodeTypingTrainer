using System.Collections.Generic;

namespace CodeTypingTrainer.Services.LanguageRules
{
    public class CppRules : BraceLanguageRules
    {
        public override string Language => "C++";
        public override int IndentSize => 4;

        public override List<string> Keywords => new()
        {
            "int", "float", "double", "char", "bool", "void", "string",
            "if", "else", "for", "while", "do", "return", "class",
            "struct", "public", "private", "protected", "new", "delete",
            "include", "namespace", "using", "std", "cout", "cin",
            "true", "false", "nullptr", "const", "auto", "vector",
            "switch", "case", "break", "continue", "try", "catch",
            "throw", "template", "typename", "virtual", "override",
            "static", "inline", "extern", "enum", "typedef", "sizeof"
        };
    }
}