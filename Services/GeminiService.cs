using System.Net.Http.Json;
using ChatBotApi.Models;

namespace ChatBotApi.Services;

/// <summary>
/// Service chịu trách nhiệm giao tiếp trực tiếp với Google Gemini REST API.
/// Triển khai từ giao diện ILlmService để tích hợp vào LlmFactory.
/// </summary>
public class GeminiService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Khởi tạo GeminiService thông qua Dependency Injection.
    /// </summary>
    /// <param name="httpClient">HttpClient được ASP.NET Core quản lý vòng đời (HttpClientFactory)</param>
    /// <param name="configuration">IConfiguration đọc cấu hình từ appsettings.json hoặc User Secrets</param>
    public GeminiService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    /// <summary>
    /// Gửi câu hỏi hiện tại kèm toàn bộ lịch sử trò chuyện đến Google Gemini API và nhận câu trả lời.
    /// </summary>
    /// <param name="userMessage">Nội dung tin nhắn / câu hỏi mới nhất từ người dùng</param>
    /// <param name="history">Danh sách các tin nhắn đã trao đổi trước đó trong cùng cuộc hội thoại</param>
    /// <returns>Chuỗi văn bản câu trả lời từ mô hình Gemini</returns>
    public async Task<string> GetResponseAsync(string userMessage, List<ChatMessage> history)
    {
        // 1. Đọc API Key và Model từ tệp cấu hình
        var apiKey = _configuration["GeminiApi:ApiKey"];
        var model = _configuration["LlmProviders:GeminiApi:Model"] ?? "gemini-2.5-flash";

        // 2. Kiểm tra nếu chưa cấu hình API Key -> Báo lỗi hướng dẫn người dùng
        if (string.IsNullOrEmpty(apiKey))
        {
            return "Chưa cấu hình GeminiApi:ApiKey. Xem hướng dẫn dotnet user-secrets trong README.";
        }

        // 3. Xây dựng URL endpoint của Google Gemini REST API
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        // 4. Báo ngữ cảnh (Context): Chuyển đổi toàn bộ lịch sử hội thoại sang định dạng DTO của Gemini API
        // Lưu ý: Role phía Gemini sử dụng là "user" và "model" (thay vì "bot" hay "assistant")
        var contents = history.Select(h => new GeminiContent
        {
            Role = h.Role == "user" ? "user" : "model",
            Parts = new List<GeminiPart> { new() { Text = h.Text } }
        }).ToList();

        // 5. Thêm câu hỏi mới nhất của người dùng vào cuối danh sách tin nhắn gửi đi
        contents.Add(new GeminiContent
        {
            Role = "user",
            Parts = new List<GeminiPart> { new() { Text = userMessage } }
        });

        // 6. Đóng gói dữ liệu vào Body Request
        var requestBody = new GeminiRequest { Contents = contents };

        // 7. Gửi HTTP POST request với payload dạng JSON
        var response = await _httpClient.PostAsJsonAsync(url, requestBody);
        
        // Đảm bảo request thành công (ném ra exception nếu mã trạng thái HTTP là 4xx hoặc 5xx)
        response.EnsureSuccessStatusCode();

        // 8. Đọc và giải mã (deserialize) dữ liệu JSON nhận được từ Gemini
        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();

        // 9. Trích xuất chuỗi văn bản phản hồi từ danh sách Candidates của Gemini
        var replyText = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        // Trả về văn bản kết quả, nếu null/rỗng thì trả về thông báo mặc định
        return replyText ?? "Xin lỗi, mình chưa nhận được phản hồi từ Gemini.";
    }
}