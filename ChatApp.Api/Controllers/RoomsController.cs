using ChatApp.Api.DTOs;
using ChatApp.Api.Hubs;
using ChatApp.Api.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ChatApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/rooms")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _rooms;
    private readonly IConnectionTracker _tracker;
    private readonly IHubContext<ChatHub> _hub;

    public RoomsController(IRoomService rooms, IConnectionTracker tracker, IHubContext<ChatHub> hub)
    {
        _rooms = rooms;
        _tracker = tracker;
        _hub = hub;
    }

    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpPost]
    public async Task<ActionResult<RoomResponse>> Create(CreateRoomRequest request)
    {
        try
        {
            var room = await _rooms.CreateRoomAsync(UserId, request.Name, request.IsDirect, request.MemberUserIds);

            // ChatHub подписывает соединение на группы комнат только в OnConnectedAsync —
            // уже подключённые участники про новую комнату не знают и без этого не получали бы
            // сообщения до переподключения. Гонки с подключением нет: хаб сначала регистрирует
            // соединение в трекере, а потом читает комнаты из БД, мы же наоборот — комната уже
            // сохранена, и только потом читаем трекер. Кто-то из двоих соединение точно увидит.
            foreach (var member in room.Members)
                foreach (var connectionId in await _tracker.GetConnectionsAsync(member.UserId))
                    await _hub.Groups.AddToGroupAsync(connectionId, room.Id.ToString());

            return CreatedAtAction(nameof(GetMyRooms), null, MapToResponse(room));
        }
        catch (InvalidOperationException ex)
        {
            // Ошибки валидации бизнес-правил (несуществующие юзеры, неверное число
            // участников для direct-диалога) — это 400, а не 500
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<RoomResponse>>> GetMyRooms()
    {
        var rooms = await _rooms.GetUserRoomsAsync(UserId);
        return Ok(rooms.Select(MapToResponse).ToList());
    }

    private static RoomResponse MapToResponse(Models.ChatRoom room) => new()
    {
        Id = room.Id,
        Name = room.Name,
        IsDirect = room.IsDirect,
        CreatedAt = room.CreatedAt,
        Members = [.. room.Members.Select(m => new RoomMemberResponse
        {
            UserId = m.UserId,
            UserName = m.User.UserName
        })]
    };
}