#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuickSurfBrowser.Services
{
    /// <summary>
    /// Представляет одно сообщение в чате
    /// </summary>
    public class ChatMessage
    {
        public string Role { get; set; } = "user"; // "user" или "assistant"
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Состояние текущего диалога (контекст)
    /// </summary>
    public class ConversationState
    {
        public string Provider { get; set; } = "Perplexity";
        public List<ChatMessage> Messages { get; set; } = new();
        public string SystemPrompt { get; set; } = "Отвечай кратко и по делу.";
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Сервис для сохранения и загрузки контекста диалога
    /// Хранит историю в %AppData%\QuickSurf\chat_context.json
    /// </summary>
    public class ChatContextService
    {
        private readonly string _filePath;
        private ConversationState _state = new();

        public ChatContextService(string dataPath)
        {
            _filePath = Path.Combine(dataPath, "chat_context.json");
        }

        /// <summary>
        /// Загружает сохранённый контекст из файла
        /// </summary>
        public void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    _state = JsonSerializer.Deserialize<ConversationState>(json) ?? new ConversationState();
                }
            }
            catch { _state = new ConversationState(); }
        }

        /// <summary>
        /// Добавляет сообщение в историю диалога
        /// </summary>
        public void AddMessage(string role, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;
            
            _state.Messages.Add(new ChatMessage { Role = role, Content = content });
            
            // Храним только последние 20 сообщений для экономии места
            if (_state.Messages.Count > 20) 
                _state.Messages.RemoveAt(0);
            
            _state.LastUpdated = DateTime.Now;
            Save();
        }

        /// <summary>
        /// Устанавливает текущего провайдера ИИ
        /// </summary>
        public void SetProvider(string provider)
        {
            if (!string.IsNullOrWhiteSpace(provider))
            {
                _state.Provider = provider;
                Save();
            }
        }

        /// <summary>
        /// Устанавливает системный промпт (инструкцию для ИИ)
        /// </summary>
        public void SetSystemPrompt(string prompt)
        {
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                _state.SystemPrompt = prompt;
                Save();
            }
        }

        /// <summary>
        /// Возвращает текущее состояние диалога
        /// </summary>
        public ConversationState GetCurrent() => _state;

        /// <summary>
        /// Возвращает количество сообщений в контексте
        /// </summary>
        public int MessageCount => _state.Messages?.Count ?? 0;

        /// <summary>
        /// Очищает весь контекст диалога
        /// </summary>
        public void Clear()
        {
            _state = new ConversationState();
            if (File.Exists(_filePath)) File.Delete(_filePath);
        }

        /// <summary>
        /// Сохраняет контекст в файл (синхронно, т.к. файл маленький)
        /// </summary>
        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
                var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }
    }
}