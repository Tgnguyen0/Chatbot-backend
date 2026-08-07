using ChatBotApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatBotApi.Controllers;

/// <summary>
/// Controller xử lý các tác vụ liên quan đến Quản lý Cuộc trò chuyện (Conversations) 
/// và Lịch sử tin nhắn (Messages) trong cơ sở dữ liệu.
/// </summary>
[ApiController]
[Route("api/conversations")]
public class ConversationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Inject DbContext để thao tác dữ liệu và IConfiguration để đọc file appsettings.json
    /// </summary>
    public ConversationController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    /// <summary>
    /// [GET /api/conversations] Lấy danh sách tất cả các cuộc trò chuyện để hiển thị lên Sidebar.
    /// Sắp xếp cuộc trò chuyện có cập nhật mới nhất lên đầu.
    /// </summary>
    /// <returns>Danh sách cuộc trò chuyện gọn (Id, Title, CreatedAt, UpdatedAt)</returns>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        // Truy vấn danh sách cuộc trò chuyện, sắp xếp giảm dần theo UpdatedAt
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

    /// <summary>
    /// [GET /api/conversations/{id}/messages] Lấy toàn bộ lịch sử tin nhắn của một cuộc trò chuyện.
    /// </summary>
    /// <param name="id">ID của cuộc trò chuyện cần tải tin nhắn</param>
    /// <returns>Danh sách tin nhắn được sắp xếp tăng dần theo thời gian tạo</returns>
    [HttpGet("{id}/messages")]
    public async Task<IActionResult> GetMessages(int id)
    {
        // 1. Kiểm tra cuộc trò chuyện có tồn tại hay không trước khi nạp tin nhắn
        var exists = await _db.Conversations.AnyAsync(c => c.Id == id);
        if (!exists) return NotFound();

        // 2. Lấy danh sách tin nhắn thuộc ConversationId truyền vào
        var messages = await _db.Messages
            .Where(m => m.ConversationId == id)
            .OrderBy(m => m.CreatedAt) // Sắp xếp tin nhắn cũ nhất lên trên, mới nhất ở dưới
            .Select(m => new { m.Id, m.Role, m.Text, m.CreatedAt })
            .ToListAsync();

        return Ok(messages);
    }

    /// <summary>
    /// [POST /api/conversations] Tạo một cuộc trò chuyện mới (trống) khi người dùng bấm nút "Cuộc trò chuyện mới".
    /// </summary>
    /// <returns>Thông tin cuộc trò chuyện vừa khởi tạo</returns>
    [HttpPost]
    public async Task<IActionResult> Create()
    {
        // Khởi tạo entity Conversation mới (Title và các mốc thời gian sẽ do Entity khởi tạo mặc định)
        var conv = new Entities.Conversation();
        
        _db.Conversations.Add(conv);
        await _db.SaveChangesAsync();

        return Ok(new { conv.Id, conv.Title, conv.CreatedAt });
    }

    /// <summary>
    /// [DELETE /api/conversations/{id}] Xóa một cuộc trò chuyện cùng tất cả các tin nhắn liên quan (Cascade Delete).
    /// </summary>
    /// <param name="id">ID cuộc trò chuyện cần xoá</param>
    /// <returns>NoContent (204) nếu xoá thành công, NotFound (404) nếu ID không tồn tại</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        // Tìm cuộc trò chuyện theo ID
        var conv = await _db.Conversations.FindAsync(id);
        if (conv == null) return NotFound();

        // Xóa cuộc trò chuyện (Nếu cài đặt Cascade Delete trong Database thì tin nhắn thuộc conversation này cũng tự động bị xóa)
        _db.Conversations.Remove(conv);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// [GET /api/conversations/providers] Lấy danh sách các LLM Providers khả dụng (Gemini, ChatGPT, Claude, Groq)
    /// và xác định Provider đang được kích hoạt mặc định từ appsettings.json.
    /// </summary>
    /// <returns>Provider mặc định (active) và danh sách cấu hình (providers)</returns>
    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        // Đọc giá trị active trong appsettings.json (LlmProviders:Active), nếu không cấu hình sẽ lấy mặc định là "Gemini"
        var active = _configuration["LlmProviders:Active"] ?? "Gemini";
        
        // Khai báo danh sách các Provider hỗ trợ
        var providers = new[] { "Gemini", "ChatGpt", "Claude", "Groq" };

        return Ok(new { active, providers });
    }
}