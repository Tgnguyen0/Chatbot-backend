namespace ChatBotApi.Entities;

public class Conversation
{
    public int Id { get; set; }
    public string Title { get; set; } = "Cuộc trò chuyện mới";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
