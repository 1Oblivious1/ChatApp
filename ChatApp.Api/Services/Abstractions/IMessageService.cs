using ChatApp.Api.Models;

namespace ChatApp.Api.Services;

public interface IMessageService
{
    /// <summary>Все комнаты, в которых состоит юзер — используется при подключении к хабу
    /// для подписки на нужные SignalR-группы.</summary>
    Task<List<Guid>> GetUserRoomIdsAsync(Guid userId);

    /// <summary>Проверка членства — вызывается перед КАЖДЫМ действием в хабе,
    /// не доверяем клиенту.</summary>
    Task<bool> IsMemberAsync(Guid roomId, Guid userId);

    Task<Message> SaveMessageAsync(Guid roomId, Guid senderId, string content);

    /// <summary>Курсорная пагинация: beforeId=null — самые свежие сообщения,
    /// beforeId=X — сообщения старше X (для подгрузки истории при скролле вверх).</summary>
    Task<List<Message>> GetMessagesAsync(Guid roomId, long? beforeId, int take);
}