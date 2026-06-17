using CodeTypingTrainer.Models;
using System.Text.RegularExpressions;

namespace CodeTypingTrainer.Services.CodeNarrators
{
    public class PythonNarrator : ICodeNarrator
    {
        public string Language => "Python";

        public List<string> SplitIntoLines(string code)
        {
            var lines = new List<string>();
            foreach (var line in code.Split('\n'))
            {
                string trimmed = line.TrimEnd('\r');
                if (trimmed.Trim().Length > 0)
                    lines.Add(trimmed);
            }
            return lines;
        }

        public List<NarratedSegment> NarrateLineSegments(string codeLine)
        {
            string trimmed = codeLine.TrimStart();
            string indentPart = codeLine.Substring(0, codeLine.Length - trimmed.Length);

            var segments = TryMatchDef(indentPart, trimmed)
                ?? TryMatchFor(indentPart, trimmed)
                ?? TryMatchWhile(indentPart, trimmed)
                ?? TryMatchIf(indentPart, trimmed)
                ?? TryMatchElif(indentPart, trimmed)
                ?? TryMatchElse(indentPart, trimmed)
                ?? TryMatchReturn(indentPart, trimmed)
                ?? TryMatchAssignment(indentPart, trimmed)
                ?? FallbackWholeLine(indentPart, trimmed);

            return segments;
        }

        // def name(args):  →  ["def name", "(args):"]
        private List<NarratedSegment> TryMatchDef(string indent, string line)
        {
            var m = Regex.Match(line, @"^def\s+(\w+)\s*\((.*)\)\s*:$");
            if (!m.Success) return null;

            string name = m.Groups[1].Value;
            string args = m.Groups[2].Value.Trim();

            var result = new List<NarratedSegment>
            {
                new() { Code = $"{indent}def {name}", Phrase = $"объявить функцию {name}" }
            };

            if (string.IsNullOrEmpty(args))
            {
                result.Add(new() { Code = "():", Phrase = "без аргументов, открыть блок" });
            }
            else
            {
                string argsPhrase = string.Join(", ", args.Split(',').Select(a => a.Trim()));
                result.Add(new()
                {
                    Code = $"({args}):",
                    Phrase = $"с аргументами {argsPhrase}, открыть блок"
                });
            }
            return result;
        }

        // for x in range(n):  →  ["for x in", "range(n):"]
        private List<NarratedSegment> TryMatchFor(string indent, string line)
        {
            var m = Regex.Match(line, @"^for\s+(\w+)\s+in\s+(.+):$");
            if (!m.Success) return null;

            string var = m.Groups[1].Value;
            string iterable = m.Groups[2].Value;

            return new List<NarratedSegment>
            {
                new() { Code = $"{indent}for {var} in", Phrase = $"цикл, {var} пробегает" },
                new() { Code = $"{iterable}:", Phrase = $"{NarrateExpression(iterable)}, открыть блок" }
            };
        }

        // while condition:
        private List<NarratedSegment> TryMatchWhile(string indent, string line)
        {
            var m = Regex.Match(line, @"^while\s+(.+):$");
            if (!m.Success) return null;

            string condition = m.Groups[1].Value;

            return new List<NarratedSegment>
            {
                new() { Code = $"{indent}while", Phrase = "цикл, пока" },
                new() { Code = $" {condition}:", Phrase = $"{NarrateExpression(condition)}, открыть блок" }
            };
        }

        // if condition:
        private List<NarratedSegment> TryMatchIf(string indent, string line)
        {
            var m = Regex.Match(line, @"^if\s+(.+):$");
            if (!m.Success) return null;

            string condition = m.Groups[1].Value;

            return new List<NarratedSegment>
            {
                new() { Code = $"{indent}if", Phrase = "если" },
                new() { Code = $" {condition}:", Phrase = $"{NarrateExpression(condition)}, открыть блок" }
            };
        }

        // elif condition:
        private List<NarratedSegment> TryMatchElif(string indent, string line)
        {
            var m = Regex.Match(line, @"^elif\s+(.+):$");
            if (!m.Success) return null;

            string condition = m.Groups[1].Value;

            return new List<NarratedSegment>
            {
                new() { Code = $"{indent}elif", Phrase = "иначе если" },
                new() { Code = $" {condition}:", Phrase = $"{NarrateExpression(condition)}, открыть блок" }
            };
        }

        private List<NarratedSegment> TryMatchElse(string indent, string line)
        {
            if (line.TrimEnd() != "else:") return null;

            return new List<NarratedSegment>
            {
                new() { Code = $"{indent}else:", Phrase = "иначе, открыть блок" }
            };
        }

        // return value  →  ["return", "value"]
        private List<NarratedSegment> TryMatchReturn(string indent, string line)
        {
            var m = Regex.Match(line, @"^return\s*(.*)$");
            if (!m.Success) return null;

            string value = m.Groups[1].Value.Trim();

            if (string.IsNullOrEmpty(value))
            {
                return new List<NarratedSegment>
                {
                    new() { Code = $"{indent}return", Phrase = "вернуть управление без значения" }
                };
            }

            return new List<NarratedSegment>
            {
                new() { Code = $"{indent}return", Phrase = "вернуть" },
                new() { Code = $" {value}", Phrase = NarrateExpression(value) }
            };
        }

        // x = expr  →  ["x =", "expr"]
        private List<NarratedSegment> TryMatchAssignment(string indent, string line)
        {
            var m = Regex.Match(line, @"^([\w\[\]]+(?:\s*,\s*[\w\[\]]+)*)\s*=\s*(.+)$");
            if (!m.Success) return null;

            string left = m.Groups[1].Value.Trim();
            string right = m.Groups[2].Value.Trim();

            return new List<NarratedSegment>
            {
                new() { Code = $"{indent}{left} =", Phrase = $"{NarrateExpression(left)} равно" },
                new() { Code = $" {right}", Phrase = NarrateExpression(right) }
            };
        }

        // Если ни одна конструкция не подошла — озвучиваем строку целиком одним сегментом
        private List<NarratedSegment> FallbackWholeLine(string indent, string line)
        {
            return new List<NarratedSegment>
            {
                new() { Code = $"{indent}{line}", Phrase = NarrateExpression(line) }
            };
        }

        // Разбор выражения в фразу — без изменений из старой версии
        private string NarrateExpression(string expr)
        {
            expr = expr.Trim();

            expr = Regex.Replace(expr, @"(\w+)\[([^\]]+)\]",
                m => $"{m.Groups[1].Value} с индексом {NarrateExpression(m.Groups[2].Value)}");

            expr = Regex.Replace(expr, @"^len\(([^)]+)\)$", "длина от $1");
            expr = Regex.Replace(expr, @"^range\(([^)]+)\)$", "range от $1");

            expr = expr.Replace("==", " равно ");
            expr = expr.Replace("!=", " не равно ");
            expr = expr.Replace(">=", " больше либо равно ");
            expr = expr.Replace("<=", " меньше либо равно ");
            expr = expr.Replace(">", " больше ");
            expr = expr.Replace("<", " меньше ");
            expr = expr.Replace("+", " плюс ");
            expr = expr.Replace("-", " минус ");
            expr = expr.Replace("*", " умножить на ");
            expr = expr.Replace("//", " целочисленно делить на ");
            expr = expr.Replace("/", " делить на ");
            expr = expr.Replace("%", " остаток от деления на ");
            expr = expr.Replace(",", " запятая ");
            expr = expr.Replace("(", " открыть скобку ");
            expr = expr.Replace(")", " закрыть скобку ");

            expr = Regex.Replace(expr, @"\s+", " ").Trim();
            return expr;
        }
    }
}