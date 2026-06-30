using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CodeTypingTrainer.Services
{
    // Сервис озвучки через Google AI Studio (Gemini TTS)
    public class GoogleTtsService
    {
        private readonly HttpClient _http = new();
        private readonly string _apiKey;

        // ВАЖНО: вставь свой ключ сюда временно для теста.
        // Позже перенесём в конфиг-файл, чтобы не хранить в коде.
        private const string ApiUrl =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-preview-tts:generateContent";

        public GoogleTtsService(string apiKey)
        {
            _apiKey = apiKey;
        }

        // Озвучить текст — возвращает аудио в формате байтов (PCM) или null при ошибке
        public async Task<(byte[] audioData, string error)> SynthesizeAsync(string text)
        {
            try
            {
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[] { new { text = $"TTS the following text exactly as written, do not respond conversationally: {text}" } }
                        }
                    },
                    generationConfig = new
                    {
                        responseModalities = new[] { "AUDIO" },
                        speechConfig = new
                        {
                            voiceConfig = new
                            {
                                prebuiltVoiceConfig = new { voiceName = "Kore" }
                            }
                        }
                    }
                };

                string json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                string url = $"{ApiUrl}?key={_apiKey}";
                var response = await _http.PostAsync(url, content);

                string responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (null, $"HTTP {(int)response.StatusCode}: {responseText}");
                }

                // Разбираем JSON-ответ — ищем base64 аудиоданные
                using var doc = JsonDocument.Parse(responseText);
                var root = doc.RootElement;

                var audioBase64 = root
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("inlineData")
                    .GetProperty("data")
                    .GetString();

                byte[] audioBytes = Convert.FromBase64String(audioBase64);
                return (audioBytes, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }
    }
}