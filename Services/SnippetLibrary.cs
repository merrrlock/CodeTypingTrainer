using CodeTypingTrainer.Models;
using System.Collections.Generic;
using System.Linq;

namespace CodeTypingTrainer.Services
{
    public class SnippetLibrary
    {
        private readonly List<CodeSnippet> _snippets;

        public SnippetLibrary()
        {
            _snippets = LoadBuiltInSnippets();
        }

        // Получить фрагменты с фильтрацией
        public List<CodeSnippet> GetSnippets(string language = null, string difficulty = null)
        {
            var result = _snippets.AsEnumerable();

            if (!string.IsNullOrEmpty(language) && language != "Все")
                result = result.Where(s => s.Language == language);

            if (!string.IsNullOrEmpty(difficulty) && difficulty != "Все")
                result = result.Where(s => s.Difficulty == difficulty);

            return result.ToList();
        }

        // Получить случайный фрагмент
        public CodeSnippet GetRandom(string language = null)
        {
            var list = GetSnippets(language);
            if (list.Count == 0) return null;
            var rnd = new System.Random();
            return list[rnd.Next(list.Count)];
        }

        private List<CodeSnippet> LoadBuiltInSnippets()
        {
            var snippets = new List<CodeSnippet>
            {
                new CodeSnippet
                {
                    Title = "Сортировка пузырьком",
                    Language = "Python",
                    Category = "Алгоритмы",
                    Difficulty = "Легко",
                    Code =
@"def bubble_sort(arr):
    n = len(arr)
    for i in range(n):
        for j in range(0, n - i - 1):
            if arr[j] > arr[j + 1]:
                arr[j], arr[j + 1] = arr[j + 1], arr[j]
    return arr"
                },

                new CodeSnippet
                {
                    Title = "Бинарный поиск",
                    Language = "Python",
                    Category = "Алгоритмы",
                    Difficulty = "Легко",
                    Code =
@"def binary_search(arr, target):
    left, right = 0, len(arr) - 1
    while left <= right:
        mid = (left + right) // 2
        if arr[mid] == target:
            return mid
        elif arr[mid] < target:
            left = mid + 1
        else:
            right = mid - 1
    return -1"
                },

                new CodeSnippet
                {
                    Title = "Разворот строки",
                    Language = "JavaScript",
                    Category = "Алгоритмы",
                    Difficulty = "Легко",
                    Code =
@"function reverseString(str) {
    return str.split('').reverse().join('');
}

function isPalindrome(str) {
    const cleaned = str.toLowerCase();
    return cleaned === reverseString(cleaned);
}"
                },

                new CodeSnippet
                {
                    Title = "Стек (Stack)",
                    Language = "JavaScript",
                    Category = "Структуры данных",
                    Difficulty = "Средне",
                    Code =
@"class Stack {
    constructor() {
        this.items = [];
    }
    push(element) {
        this.items.push(element);
    }
    pop() {
        if (this.isEmpty()) return null;
        return this.items.pop();
    }
    peek() {
        return this.items[this.items.length - 1];
    }
    isEmpty() {
        return this.items.length === 0;
    }
}"
                },

                new CodeSnippet
                {
                    Title = "Слияние двух списков",
                    Language = "C++",
                    Category = "Алгоритмы",
                    Difficulty = "Средне",
                    Code =
@"#include <vector>
using namespace std;

vector<int> mergeSorted(vector<int>& a, vector<int>& b) {
    vector<int> result;
    int i = 0, j = 0;
    while (i < a.size() && j < b.size()) {
        if (a[i] <= b[j]) result.push_back(a[i++]);
        else result.push_back(b[j++]);
    }
    while (i < a.size()) result.push_back(a[i++]);
    while (j < b.size()) result.push_back(b[j++]);
    return result;
}"
                },

                new CodeSnippet
                {
                    Title = "FizzBuzz",
                    Language = "Java",
                    Category = "Алгоритмы",
                    Difficulty = "Легко",
                    Code =
@"public class FizzBuzz {
    public static void main(String[] args) {
        for (int i = 1; i <= 100; i++) {
            if (i % 15 == 0) {
                System.out.println(""FizzBuzz"");
            } else if (i % 3 == 0) {
                System.out.println(""Fizz"");
            } else if (i % 5 == 0) {
                System.out.println(""Buzz"");
            } else {
                System.out.println(i);
            }
        }
    }
}"
                }
            };

            foreach (var snippet in snippets)
                snippet.Code = snippet.Code.Replace("\r\n", "\n").Replace("\r", "\n");

            return snippets;
        }
    }
}