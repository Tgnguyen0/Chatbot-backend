using Microsoft.Extensions.DependencyInjection;

namespace ChatBotApi.Services;

/// <summary>
/// Lớp Factory (Factory Design Pattern) chịu trách nhiệm quyết định và khởi tạo 
/// đối tượng service LLM tương ứng (triển khai từ ILlmService) dựa trên tên Provider truyền vào.
/// </summary>
public class LlmFactory
{
    // DI Container dùng để giải phóng/lấy các instance service đã được đăng ký trong Program.cs
    private readonly IServiceProvider _provider;

    /// <summary>
    /// Inject IServiceProvider vào Factory để truy xuất các Service từ DI Container.
    /// </summary>
    /// <param name="provider">Service Provider của ASP.NET Core</param>
    public LlmFactory(IServiceProvider provider)
    {
        _provider = provider;
    }

    /// <summary>
    /// Khởi tạo và trả về instance cụ thể của ILlmService tương ứng với tên Provider.
    /// </summary>
    /// <param name="provider">Tên LLM Provider cần dùng ("ChatGpt", "Claude", "Groq", hoặc mặc định là "Gemini")</param>
    /// <returns>Đối tượng triển khai giao diện ILlmService (GeminiService, ChatGptService, ...)</returns>
    public ILlmService Create(string provider) => provider switch
    {
        // Trả về ChatGptService đã đăng ký trong DI
        "ChatGpt" => _provider.GetRequiredService<ChatGptService>(),

        // Trả về ClaudeService đã đăng ký trong DI
        "Claude"  => _provider.GetRequiredService<ClaudeService>(),

        // Trả về GroqService đã đăng ký trong DI
        "Groq"    => _provider.GetRequiredService<GroqService>(),

        // Trường hợp mặc định (Fallback): Sử dụng GeminiService nếu truyền chuỗi không khớp hoặc null
        _         => _provider.GetRequiredService<GeminiService>(),
    };
}