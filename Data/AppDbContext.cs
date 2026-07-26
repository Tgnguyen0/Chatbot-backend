using ChatBotApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChatBotApi.Data;

public internal class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conversation>(e =>
        {
            e.ToTable("conversations");        // ← thêm dòng này
            e.HasKey(c => c.Id);
            e.Property(c => c.Title).HasMaxLength(200);
        });

        modelBuilder.Entity<Message>(e =>
        {
            e.ToTable("messages");             // ← thêm dòng này
            e.HasKey(m => m.Id);
            e.Property(m => m.Role).HasMaxLength(10);
            e.Property(m => m.Text).HasColumnType("longtext");

            e.HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
