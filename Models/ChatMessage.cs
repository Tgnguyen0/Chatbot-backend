namespace ChatBotApi.Models;

public class ChatMessage
{
    public string Role { get; set; } = string.Empty; // "user" hoặc "model"
    public string Text { get; set; } = string.Empty;
}
