using ChatApp.Client.DTOs;
using Microsoft.AspNetCore.SignalR.Client;

namespace ChatApp.Client.Services;

public class HubClient : IAsyncDisposable
{
    private readonly HubConnection _connection;

    public event Action<HubMessage>? MessageReceived;
    public event Action<Guid>? UserOnline;
    public event Action<Guid>? UserOffline;
    public event Action<Guid, Guid>? UserTyping; // (userId, roomId)

    public HubClient(string baseUrl, Func<Task<string?>> getAccessToken)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/chat", options =>
            {
                // AccessTokenProvider вызывается заново при каждой (пере)попытке
                // подключения — getAccessToken при необходимости сам обновит
                // протухший токен через refresh, так что реконнект после долгой
                // работы не упрётся в 401.
                options.AccessTokenProvider = getAccessToken;
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<HubMessage>("ReceiveMessage", msg => MessageReceived?.Invoke(msg));
        _connection.On<Guid>("UserOnline", userId => UserOnline?.Invoke(userId));
        _connection.On<Guid>("UserOffline", userId => UserOffline?.Invoke(userId));
        _connection.On<Guid, Guid>("UserTyping", (userId, roomId) => UserTyping?.Invoke(userId, roomId));
    }

    public HubConnectionState State => _connection.State;

    public Task StartAsync() => _connection.StartAsync();

    public Task SendMessageAsync(Guid roomId, string content)
        => _connection.InvokeAsync("SendMessage", roomId, content);

    public Task JoinRoomAsync(Guid roomId)
        => _connection.InvokeAsync("JoinRoom", roomId);

    public Task NotifyTypingAsync(Guid roomId)
        => _connection.InvokeAsync("Typing", roomId);

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}