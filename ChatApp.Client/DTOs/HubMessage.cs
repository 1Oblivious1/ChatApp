namespace ChatApp.Client.DTOs;

// Модель для события ReceiveMessage из хаба — по составу отличается от MessageResponse
// (хаб не шлёт SenderUserName, только SenderId — см. ChatHub.SendMessage)
public class HubMessage
{
    public long Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = default!;
    public DateTime SentAt { get; set; }
}