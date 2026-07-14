using System.Net.Http.Json;
using ChatBotApi.Models;

namespace ChatBotApi.Services;

public class GeminiService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GeminiService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> GetResponseAsync(string userMessage, List<ChatMessage> history)
    {
        var apiKey = _configuration["GeminiApi:ApiKey"];
        var model = _configuration["LlmProviders:GeminiApi:Model"] ?? "gemini-2.5-flash";

        if (string.IsNullOrEmpty(apiKey))
        {
            return "Chưa cấu hình GeminiApi:ApiKey. Xem hướng dẫn dotnet user-secrets trong README.";
        }

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        // Gửi kèm lịch sử hội thoại để Gemini hiểu context trước đó
        var contents = history.Select(h => new GeminiContent
        {
            Role = h.Role == "user" ? "user" : "model",
            Parts = new List<GeminiPart> { new() { Text = h.Text } }
        }).ToList();

        contents.Add(new GeminiContent
        {
            Role = "user",
            Parts = new List<GeminiPart> { new() { Text = userMessage } }
        });

        var requestBody = new GeminiRequest { Contents = contents };

        var response = await _httpClient.PostAsJsonAsync(url, requestBody);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();

        var replyText = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        return replyText ?? "Xin lỗi, mình chưa nhận được phản hồi từ Gemini.";
    }
}
