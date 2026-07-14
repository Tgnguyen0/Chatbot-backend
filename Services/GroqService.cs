using System.Net.Http.Json;
using System.Text.Json;
using ChatBotApi.Models;

namespace ChatBotApi.Services;

public class GroqService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GroqService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> GetResponseAsync(string userMessage, List<ChatMessage> history)
    {
        var apiKey = _configuration["GroqApi:ApiKey"];
        var model  = _configuration["LlmProviders:Groq:Model"] ?? "llama-3.3-70b-versatile";

        var messages = history.Select(h => new
        {
            role    = h.Role == "model" ? "assistant" : "user",
            content = h.Text
        }).ToList<object>();

        messages.Add(new { role = "user", content = userMessage });

        var body = new { model, messages };

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var response = await _httpClient.PostAsJsonAsync(
            "https://api.groq.com/openai/v1/chat/completions", body);  // ← URL Groq

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "Không có phản hồi.";
    }
}