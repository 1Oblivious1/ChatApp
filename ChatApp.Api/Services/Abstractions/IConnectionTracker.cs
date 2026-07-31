namespace ChatApp.Api.Services.Abstractions;

public interface IConnectionTracker
{
    /// <summary>Добавляет соединение пользователя. Возвращает true, если это ПЕРВОЕ
    /// активное соединение юзера (то есть он только что стал "онлайн").</summary>
    Task<bool> AddConnectionAsync(Guid userId, string connectionId);

    /// <summary>Убирает соединение. Возвращает true, если у юзера ОСТАЛИСЬ активные
    /// соединения (например, открыта другая вкладка), false — если это было последнее
    /// и юзер теперь оффлайн.</summary>
    Task<bool> RemoveConnectionAsync(Guid userId, string connectionId);
}