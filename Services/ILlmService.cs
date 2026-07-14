using ChatBotApi.Models;

namespace ChatBotApi.Services;

public interface ILlmService
{
    Task<string> GetResponseAsync(string userMessage, List<ChatMessage> history);
}