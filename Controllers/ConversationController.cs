using ChatBotApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatBotApi.Controllers;

[ApiController]
[Route("api/conversations")]
public class ConversationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public ConversationController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    // GET /api/conversations — danh sách các cuộc trò chuyện (sidebar)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.Conversations
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.CreatedAt,
                c.UpdatedAt
            })
            .ToListAsync();

        return Ok(list);
    }

    // GET /api/conversations/{id}/messages — load lịch sử tin nhắn
    [HttpGet("{id}/messages")]
    public async Task<IActionResult> GetMessages(int id)
    {
        var exists = await _db.Conversations.AnyAsync(c => c.Id == id);
        if (!exists) return NotFound();

        var messages = await _db.Messages
            .Where(m => m.ConversationId == id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.Id, m.Role, m.Text, m.CreatedAt })
            .ToListAsync();

        return Ok(messages);
    }

    // POST /api/conversations — tạo cuộc trò chuyện mới
    [HttpPost]
    public async Task<IActionResult> Create()
    {
        var conv = new Entities.Conversation();
        _db.Conversations.Add(conv);
        await _db.SaveChangesAsync();
        return Ok(new { conv.Id, conv.Title, conv.CreatedAt });
    }

    // DELETE /api/conversations/{id} — xoá cuộc trò chuyện
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var conv = await _db.Conversations.FindAsync(id);
        if (conv == null) return NotFound();

        _db.Conversations.Remove(conv);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        var active = _configuration["LlmProviders:Active"] ?? "Gemini";
        var providers = new[] { "Gemini", "ChatGpt", "Claude", "Groq" };
        return Ok(new { active, providers });
    }
}
