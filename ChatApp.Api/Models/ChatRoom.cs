namespace ChatApp.Api.Models;

public class ChatRoom
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public bool IsDirect { get; set; } // приватный диалог 1-на-1 или групповой
    public DateTime CreatedAt { get; set; }
    public ICollection<RoomMember> Members { get; set; } = new List<RoomMember>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}