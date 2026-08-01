using ChatApp.Api.Models;

namespace ChatApp.Api.Services.Abstractions;

public interface IRoomService
{
    Task<ChatRoom> CreateRoomAsync(Guid creatorId, string? name, bool isDirect, List<Guid> memberUserIds);

    /// <summary>Комнаты пользователя вместе с участниками (для отображения списка чатов).</summary>
    Task<List<ChatRoom>> GetUserRoomsAsync(Guid userId);
}