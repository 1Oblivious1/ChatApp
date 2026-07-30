namespace ChatApp.Api.Models;

public class ChatRoom
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Для группового чата — название; для приватного диалога 1-на-1 можно оставить null
    // и строить отображаемое имя на клиенте из имени собеседника
    public string? Name { get; set; }

    public bool IsDirect { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RoomMember> Members { get; set; } = new List<RoomMember>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}