namespace ChatApp.Api.Models;

public class RoomMember
{
    public Guid RoomId { get; set; }
    public ChatRoom Room { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Для реализации "прочитано" / счётчика непрочитанных без отдельной таблицы
    public DateTime? LastReadAt { get; set; }

    public RoomRole Role { get; set; } = RoomRole.Member;
}

public enum RoomRole
{
    Member = 0,
    Admin = 1
}