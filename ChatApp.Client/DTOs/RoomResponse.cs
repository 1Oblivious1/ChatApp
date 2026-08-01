namespace ChatApp.Client.DTOs;

public class RoomResponse
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public bool IsDirect { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<RoomMemberResponse> Members { get; set; } = new();
}

public class RoomMemberResponse
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = default!;
}