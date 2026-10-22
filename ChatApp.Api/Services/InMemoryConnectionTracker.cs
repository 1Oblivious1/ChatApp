using ChatApp.Api.Services.Abstractions;
using System.Collections.Concurrent;

namespace ChatApp.Api.Services;

/// <summary>
/// Хранит соединения в памяти процесса. Работает только пока приложение запущено
/// в ОДНОМ инстансе — у каждого юзера может быть несколько ConnectionId (вкладки/устройства),
/// поэтому храним HashSet, а не одно значение.
///
/// При переходе на несколько инстансов (масштабирование) эту реализацию нужно заменить
/// на Redis-based (например, через IConnectionMultiplexer), иначе presence будет врать —
/// каждый инстанс будет знать только о своих соединениях.
/// </summary>
public class InMemoryConnectionTracker : IConnectionTracker
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();
    private readonly object _lock = new();

    public Task<bool> AddConnectionAsync(Guid userId, string connectionId)
    {
        lock (_lock)
        {
            var isFirstConnection = false;

            if (!_connections.TryGetValue(userId, out var set))
            {
                set = [];
                _connections[userId] = set;
                isFirstConnection = true;
            }

            set.Add(connectionId);
            return Task.FromResult(isFirstConnection);
        }
    }

    public Task<bool> RemoveConnectionAsync(Guid userId, string connectionId)
    {
        lock (_lock)
        {
            if (!_connections.TryGetValue(userId, out var set))
                return Task.FromResult(false); // юзера и так не было в словаре

            set.Remove(connectionId);

            if (set.Count == 0)
            {
                _connections.TryRemove(userId, out _);
                return Task.FromResult(false); // это было последнее соединение -> юзер оффлайн
            }

            return Task.FromResult(true); // остались другие соединения -> юзер всё ещё онлайн
        }
    }

    public Task<IReadOnlyList<string>> GetConnectionsAsync(Guid userId)
    {
        lock (_lock)
        {
            // Возвращаем копию: HashSet меняется под локом из хаба, а вызывающий код
            // будет перебирать результат уже после выхода из lock
            IReadOnlyList<string> result = _connections.TryGetValue(userId, out var set)
                ? [.. set]
                : [];

            return Task.FromResult(result);
        }
    }
}