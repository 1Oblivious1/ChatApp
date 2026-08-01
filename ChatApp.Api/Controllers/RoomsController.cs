using ChatApp.Api.DTOs;
using ChatApp.Api.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/rooms")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _rooms;

    public RoomsController(IRoomService rooms)
    {
        _rooms = rooms;
    }

    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpPost]
    public async Task<ActionResult<RoomResponse>> Create(CreateRoomRequest request)
    {
        try
        {
            var room = await _rooms.CreateRoomAsync(UserId, request.Name, request.IsDirect, request.MemberUserIds);

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