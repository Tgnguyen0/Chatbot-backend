using ChatBotApi.Data;
using ChatBotApi.Entities;
using ChatBotApi.Models;
using ChatBotApi.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatBotApi.Hubs;

public class ChatHub : Hub
{
    private readonly LlmFactory _llmFactory;
    private readonly AppDbContext _db;

    public ChatHub(LlmFactory llmFactory, AppDbContext db)
    {
        _llmFactory = llmFactory;
        _db = db;
    }

    public async Task SendMessage(int conversationId, string message, string provider = "Gemini")
    {
        var llm = _llmFactory.Create(provider);
        var conversation = await _db.Conversations.FindAsync(conversationId);
        if (conversation == null)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "error", "Conversation không tồn tại.");
            return;
        }

        var history = await _db.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessage { Role = m.Role, Text = m.Text })
            .ToListAsync();

        try
        {
            var reply = await llm.GetResponseAsync(message, history);

            _db.Messages.AddRange(
                new Message { ConversationId = conversationId, Role = "user",  Text = message },
                new Message { ConversationId = conversationId, Role = "model", Text = reply }
            );

            if (!history.Any())
                conversation.Title = message.Length > 60 ? message[..60] + "..." : message;

            conversation.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await Clients.Caller.SendAsync("ReceiveMessage", "bot", reply);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "error", "Lỗi Gemini: " + ex.Message);
        }
    }
}
