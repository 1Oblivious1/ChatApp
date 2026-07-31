using ChatApp.Api.Models;

namespace ChatApp.Api.Services.Abstractions;

public interface IMessageService
{
    /// <summary>Все комнаты, в которых состоит юзер — используется при подключении к хабу
    /// для подписки на нужные SignalR-группы.</summary>
    Task<List<Guid>> GetUserRoomIdsAsync(Guid userId);

    /// <summary>Проверка членства — вызывается перед КАЖДЫМ действием в хабе,
    /// не доверяем клиенту.</summary>
    Task<bool> IsMemberAsync(Guid roomId, Guid userId);

    Task<Message> SaveMessageAsync(Guid roomId, Guid senderId, string content);
}