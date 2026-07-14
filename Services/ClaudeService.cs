using System.Net.Http.Json;
using System.Text.Json;
using ChatBotApi.Models;

namespace ChatBotApi.Services;

public class ClaudeService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public ClaudeService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> GetResponseAsync(string userMessage, List<ChatMessage> history)
    {
        var apiKey = _configuration["ClaudeApi:ApiKey"];
        var model  = _configuration["LlmProviders:Claude:Model"] ?? "claude-sonnet-4-6";

        var messages = history.Select(h => new
        {
            role    = h.Role == "model" ? "assistant" : "user",
            content = h.Text
        }).ToList<object>();

        messages.Add(new { role = "user", content = userMessage });

        var body = new { model, max_tokens = 1024, messages };

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var response = await _httpClient.PostAsJsonAsync(
            "https://api.anthropic.com/v1/messages", body);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? "Không có phản hồi.";
    }
}