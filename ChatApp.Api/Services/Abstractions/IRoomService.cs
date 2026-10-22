using ChatApp.Api.Models;

namespace ChatApp.Api.Services.Abstractions;

public interface IRoomService
{
    Task<ChatRoom> CreateRoomAsync(Guid creatorId, string? name, bool isDirect, List<Guid> memberUserIds);

    /// <summary>Комнаты пользователя вместе с участниками (для отображения списка чатов).</summary>
    Task<List<ChatRoom>> GetUserRoomsAsync(Guid userId);

    /// <summary>Пользователи, у которых есть хотя бы одна общая комната с userId (без него самого) —
    /// только им положено видеть, что он онлайн.</summary>
    Task<List<Guid>> GetContactIdsAsync(Guid userId);
}