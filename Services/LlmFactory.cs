namespace ChatBotApi.Services;

public class LlmFactory
{
    private readonly IServiceProvider _provider;

    public LlmFactory(IServiceProvider provider)
    {
        _provider = provider;
    }

    public ILlmService Create(string provider) => provider switch
    {
        "ChatGpt" => _provider.GetRequiredService<ChatGptService>(),
        "Claude"  => _provider.GetRequiredService<ClaudeService>(),
        "Groq"    => _provider.GetRequiredService<GroqService>(),
        _          => _provider.GetRequiredService<GeminiService>(),
    };
}