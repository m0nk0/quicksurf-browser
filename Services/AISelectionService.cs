using System;
using System.Threading.Tasks;

namespace QuickSurfBrowser.Services
{
    public class AISelectionService
    {
        private readonly AiWorkerService _aiWorker;
        private readonly ChatContextService _chatContext;
        private readonly Action<string, bool, bool> _addChatMessage;
        private readonly Action<string> _setChatInput;
        private readonly Action _openAISidebar;
        private readonly Action _focusChatInput;

        public AISelectionService(
            AiWorkerService aiWorker,
            ChatContextService chatContext,
            Action<string, bool, bool> addChatMessage,
            Action<string> setChatInput,
            Action openAISidebar,
            Action focusChatInput)
        {
            _aiWorker = aiWorker;
            _chatContext = chatContext;
            _addChatMessage = addChatMessage;
            _setChatInput = setChatInput;
            _openAISidebar = openAISidebar;
            _focusChatInput = focusChatInput;
        }

        public async void ProcessSelection(string selectedText, string actionType)
        {
            if (string.IsNullOrWhiteSpace(selectedText) || selectedText == "undefined")
                return;

            string prompt = "";
            switch (actionType)
            {
                case "translate":
                    prompt = $"Переведи на русский язык: {selectedText}";
                    break;
                case "explain":
                    prompt = $"Объясни простыми словами: {selectedText}";
                    break;
                case "checkcode":
                    prompt = $"Найди ошибки в этом коде и исправь их:\n\n{selectedText}";
                    break;
                case "summarize":
                    prompt = $"Кратко перескажи: {selectedText}";
                    break;
                case "ask":
                    prompt = selectedText;
                    break;
                default:
                    prompt = selectedText;
                    break;
            }

            // Открываем AI панель
            _openAISidebar();
            
            // НЕ вставляем текст в поле ввода (убираем эту строку)
            // _setChatInput(prompt);
            
            // Просто фокусируемся на поле ввода (опционально)
            _focusChatInput();
            
            // Отправляем сообщение пользователя (без дублирования в поле ввода)
            _addChatMessage(prompt, true, false);
            _chatContext.AddMessage("user", prompt);
            
            // Добавляем индикатор загрузки
            _addChatMessage("Думаю...", false, true);
            
            try
            {
                var response = await _aiWorker.AskAsync(prompt);
                _addChatMessage(response, false, false);
                _chatContext.AddMessage("assistant", response);
            }
            catch (Exception ex)
            {
                _addChatMessage($"⚠️ {ex.Message}\n\n💡 Убедитесь, что вы вошли в Яндекс в основной вкладке.", false, false);
            }
        }
    }
}