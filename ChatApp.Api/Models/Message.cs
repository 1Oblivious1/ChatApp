namespace ChatApp.Api.Models;

public class Message
{
    public long Id { get; set; } // bigint identity — их будет много
    public Guid RoomId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = default!;
    public DateTime SentAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
}
