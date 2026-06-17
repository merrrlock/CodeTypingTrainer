namespace CodeTypingTrainer.Services.LanguageRules
{
    public class JavaRules : BraceLanguageRules
    {
        public override string Language => "Java";
        public override int IndentSize => 4;

        public override List<string> Keywords => new()
        {
            "public", "private", "protected", "class", "interface",
            "extends", "implements", "new", "return", "if", "else",
            "for", "while", "do", "try", "catch", "finally", "throw",
            "throws", "static", "final", "void", "int", "boolean",
            "String", "true", "false", "null", "this", "super",
            "switch", "case", "break", "continue", "import", "package",
            "abstract", "enum", "instanceof", "synchronized", "volatile"
        };
    }
}