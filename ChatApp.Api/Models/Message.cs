namespace ChatApp.Api.Models;

public class Message
{
    public long Id { get; set; }

    public Guid RoomId { get; set; }
    public ChatRoom Room { get; set; } = default!;

    public Guid SenderId { get; set; }
    public User Sender { get; set; } = default!;

    public string Content { get; set; } = default!;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
}