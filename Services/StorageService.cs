using CodeTypingTrainer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CodeTypingTrainer.Services
{
    // Отвечает за сохранение и загрузку профиля и истории сессий на диск
    public class StorageService
    {
        private readonly string _appDataFolder;
        private readonly string _profilePath;
        private readonly string _sessionsFolder;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };

        public StorageService()
        {
            _appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CodeTypingTrainer");

            _profilePath = Path.Combine(_appDataFolder, "profile.json");
            _sessionsFolder = Path.Combine(_appDataFolder, "sessions");

            // Создаём папки если их ещё нет
            Directory.CreateDirectory(_appDataFolder);
            Directory.CreateDirectory(_sessionsFolder);
        }

        // -------------------------------------------------------
        // ПРОФИЛЬ
        // -------------------------------------------------------

        public UserProfile LoadProfile()
        {
            if (!File.Exists(_profilePath))
                return new UserProfile();

            try
            {
                string json = File.ReadAllText(_profilePath);
                return JsonSerializer.Deserialize<UserProfile>(json) ?? new UserProfile();
            }
            catch
            {
                // Файл повреждён или несовместимый формат — начинаем с чистого профиля
                return new UserProfile();
            }
        }

        public void SaveProfile(UserProfile profile)
        {
            try
            {
                string json = JsonSerializer.Serialize(profile, _jsonOptions);
                File.WriteAllText(_profilePath, json);
            }
            catch
            {
                // Если не удалось сохранить — не блокируем работу приложения
                // В будущем можно добавить уведомление пользователю
            }
        }

        // -------------------------------------------------------
        // ИСТОРИЯ СЕССИЙ
        // -------------------------------------------------------

        public void SaveSessionRecord(SessionRecord record)
        {
            try
            {
                string fileName = $"session_{record.Timestamp:yyyy-MM-dd_HHmmss}.json";
                string path = Path.Combine(_sessionsFolder, fileName);

                string json = JsonSerializer.Serialize(record, _jsonOptions);
                File.WriteAllText(path, json);
            }
            catch
            {
                // Аналогично — не блокируем работу при ошибке записи
            }
        }

        // Загрузить всю историю сессий — для будущих графиков статистики
        public List<SessionRecord> LoadAllSessions()
        {
            var result = new List<SessionRecord>();

            if (!Directory.Exists(_sessionsFolder))
                return result;

            foreach (var file in Directory.GetFiles(_sessionsFolder, "session_*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var record = JsonSerializer.Deserialize<SessionRecord>(json);
                    if (record != null)
                        result.Add(record);
                }
                catch
                {
                    // Пропускаем повреждённые файлы
                }
            }

            result.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            return result;
        }

        // Полностью очищает профиль и историю сессий
        public void ClearAllData()
        {
            try
            {
                // Удаляем все файлы сессий
                if (Directory.Exists(_sessionsFolder))
                {
                    foreach (var file in Directory.GetFiles(_sessionsFolder, "session_*.json"))
                        File.Delete(file);
                }

                // Удаляем файл профиля
                if (File.Exists(_profilePath))
                    File.Delete(_profilePath);
            }
            catch
            {
                // Не блокируем работу приложения при ошибке удаления
            }
        }
    }
}