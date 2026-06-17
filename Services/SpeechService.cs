using System.Speech.Synthesis;

namespace CodeTypingTrainer.Services
{
    // Обёртка над синтезатором речи Windows (SAPI)
    public class SpeechService
    {
        private readonly SpeechSynthesizer _synth;

        public SpeechService()
        {
            _synth = new SpeechSynthesizer();

            // Настройка голоса — пробуем найти русский голос,
            // если нет — остаётся системный по умолчанию
            try
            {
                _synth.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet);
            }
            catch
            {
                // Если что-то пошло не так — используем голос по умолчанию
            }

            _synth.Rate = 0;    // скорость речи: -10 (медленно) ... 10 (быстро)
            _synth.Volume = 100; // громкость: 0-100
        }

        // Текущая скорость в виде множителя (0.5x - 2x) для UI
        public double SpeedMultiplier
        {
            get => (_synth.Rate + 10) / 10.0; // переводим -10..10 в 0..2
            set => _synth.Rate = (int)((value * 10) - 10);
        }

        public int Volume
        {
            get => _synth.Volume;
            set => _synth.Volume = value;
        }

        // Озвучить текст синхронно (блокирует поток — используем асинхронно)
        public void Speak(string text)
        {
            _synth.Speak(text);
        }

        // Озвучить текст асинхронно — не блокирует UI
        public void SpeakAsync(string text)
        {
            _synth.SpeakAsync(text);
        }

        // Поставить на паузу
        public void Pause()
        {
            _synth.Pause();
        }

        // Снять с паузы
        public void Resume()
        {
            _synth.Resume();
        }

        // Остановить и очистить очередь озвучки
        public void StopAll()
        {
            _synth.SpeakAsyncCancelAll();
        }

        // Получить список доступных голосов (для настроек)
        public System.Collections.Generic.List<string> GetAvailableVoices()
        {
            var voices = new System.Collections.Generic.List<string>();
            foreach (var voice in _synth.GetInstalledVoices())
            {
                if (voice.Enabled)
                    voices.Add(voice.VoiceInfo.Name);
            }
            return voices;
        }

        public void SetVoice(string voiceName)
        {
            try
            {
                _synth.SelectVoice(voiceName);
            }
            catch
            {
                // Голос не найден — оставляем текущий
            }
        }
    }
}