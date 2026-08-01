namespace ChatApp.Client.DTOs;

public class MessageResponse
{
    public long Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderUserName { get; set; } = default!;
    public string Content { get; set; } = default!;
    public DateTime SentAt { get; set; }
}