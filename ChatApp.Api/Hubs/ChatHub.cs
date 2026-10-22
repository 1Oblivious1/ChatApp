using ChatApp.Api.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ChatApp.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMessageService _messages;
    private readonly IRoomService _rooms;
    private readonly IConnectionTracker _tracker;

    public ChatHub(IMessageService messages, IRoomService rooms, IConnectionTracker tracker)
    {
        _messages = messages;
        _rooms = rooms;
        _tracker = tracker;
    }

    // NameIdentifier заполняется в TokenService.GenerateAccessToken при выпуске JWT
    private Guid UserId => Guid.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    public override async Task OnConnectedAsync()
    {
        var isFirstConnection = await _tracker.AddConnectionAsync(UserId, Context.ConnectionId);

        // Подписываем это соединение на группы всех комнат пользователя, чтобы
        // SendMessage мог рассылать через Clients.Group(roomId) без дополнительных JoinRoom-вызовов
        var roomIds = await _messages.GetUserRoomIdsAsync(UserId);
        foreach (var roomId in roomIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId.ToString());

        // Оповещаем остальных, только если юзер только что стал онлайн — если это вторая
        // вкладка того же юзера, для остальных участников чата ничего не изменилось
        if (isFirstConnection)
            await NotifyContactsAsync("UserOnline");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var stillOnline = await _tracker.RemoveConnectionAsync(UserId, Context.ConnectionId);

        if (!stillOnline)
            await NotifyContactsAsync("UserOffline");

        await base.OnDisconnectedAsync(exception);
    }

    private async Task NotifyContactsAsync(string method)
    {
        // Не Clients.Others: тогда статус любого юзера видели бы вообще все подключённые,
        // включая тех, с кем у него нет ни одного общего чата. Clients.Users, а не
        // Clients.Groups по комнатам — чтобы юзер из нескольких общих комнат не получил
        // одно и то же событие несколько раз. Id юзера для SignalR берётся из того же
        // NameIdentifier-клейма (DefaultUserIdProvider).
        var contactIds = await _rooms.GetContactIdsAsync(UserId);
        if (contactIds.Count == 0)
            return;

        await Clients.Users([.. contactIds.Select(id => id.ToString())]).SendAsync(method, UserId);
    }

    public async Task SendMessage(Guid roomId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new HubException("Сообщение не может быть пустым");

        if (content.Length > 4000)
            throw new HubException("Сообщение слишком длинное");

        // Критично: никогда не доверяем roomId от клиента без проверки членства —
        // иначе любой авторизованный юзер сможет писать в чужие комнаты
        if (!await _messages.IsMemberAsync(roomId, UserId))
            throw new HubException("Вы не являетесь участником этой комнаты");

        var message = await _messages.SaveMessageAsync(roomId, UserId, content);

        await Clients.Group(roomId.ToString()).SendAsync("ReceiveMessage", new
        {
            message.Id,
            message.RoomId,
            message.SenderId,
            message.Content,
            message.SentAt
        });
    }

    public async Task JoinRoom(Guid roomId)
    {
        if (!await _messages.IsMemberAsync(roomId, UserId))
            throw new HubException("Вы не являетесь участником этой комнаты");

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId.ToString());
    }

    public async Task Typing(Guid roomId)
    {
        // Проверяем членство и здесь для консистентности — стоимость незначительна
        // (это point-lookup по индексу первичного ключа RoomMember), а без проверки
        // пришлось бы отдельно объяснять читателю кода, почему тут исключение из правила.
        if (!await _messages.IsMemberAsync(roomId, UserId))
            return;

        await Clients.OthersInGroup(roomId.ToString()).SendAsync("UserTyping", UserId, roomId);
    }
}