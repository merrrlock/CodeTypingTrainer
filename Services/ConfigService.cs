using System;
using System.IO;
using System.Text.Json;

namespace CodeTypingTrainer.Services
{
    // Простой сервис для чтения настроек из appsettings.json
    public class ConfigService
    {
        private readonly JsonElement _root;

        public ConfigService()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Файл appsettings.json не найден. Убедитесь что он скопирован в папку с приложением " +
                    "и его свойство 'Копировать в выходной каталог' установлено в 'Копировать, если новее'.",
                    path);
            }

            string json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            _root = doc.RootElement.Clone();
        }

        // Получить ключ Google TTS API
        public string GetGoogleTtsApiKey()
        {
            return _root
                .GetProperty("GoogleTts")
                .GetProperty("ApiKey")
                .GetString();
        }
    }
}