using ChatBotApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChatBotApi.Data;

/// <summary> 
/// DbContext đại diện cho kết nối giữa ứng dụng và cơ sở dữ liệu.
/// Chứa các DbSet và cấu hình mapping Entity -> Table. 
/// </summary>
public class AppDbContext : DbContext
{
    // Constructor nhận cấu hình DbContext từ Program.cs
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Bảng conversations
    public DbSet<Conversation> Conversations => Set<Conversation>();
    // Bảng messages
    public DbSet<Message> Messages => Set<Message>();

    /// <summary> 
    /// Cấu hình Entity bằng Fluent API. 
    /// Dùng để đặt tên bảng, khóa chính, kiểu dữ liệu, quan hệ... 
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ========================= 
        // Cấu hình bảng Conversation 
        // =========================
        modelBuilder.Entity<Conversation>(e =>
        {
            // Đặt tên bảng trong database
            e.ToTable("conversations");        // ← thêm dòng này

            // Khóa chính
            e.HasKey(c => c.Id);

            // Giới hạn độ dài tiêu đề tối đa 200 ký tự
            e.Property(c => c.Title).HasMaxLength(200);
        });

        // ===================== 
        // Cấu hình bảng Message 
        // =====================
        modelBuilder.Entity<Message>(e =>
        {
            // Đặt tên bảng trong database
            e.ToTable("messages");             // ← thêm dòng này

            // Khóa chính
            e.HasKey(m => m.Id);

            // Role chỉ lưu tối đa 10 ký tự 
            // Ví dụ: user, assistant, system
            e.Property(m => m.Role).HasMaxLength(10);

            // Nội dung tin nhắn dùng kiểu longtext để lưu văn bản dài
            e.Property(m => m.Text).HasColumnType("longtext");

            // Quan hệ 1 - N:
            // 1 Conversation có nhiều Message
            // 1 Message thuộc về 1 Conversation
            e.HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)

            // Khi xóa Conversation thì tự động xóa toàn bộ Message liên quan
            .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
