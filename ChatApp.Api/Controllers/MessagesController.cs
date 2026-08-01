using ChatApp.Api.DTOs;
using ChatApp.Api.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/rooms/{roomId:guid}/messages")]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messages;

    public MessagesController(IMessageService messages)
    {
        _messages = messages;
    }

    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<ActionResult<List<MessageResponse>>> GetHistory(
        Guid roomId,
        [FromQuery] long? beforeId,
        [FromQuery] int take = 50)
    {
        // Та же проверка членства, что и в ChatHub.SendMessage — REST и SignalR это
        // два независимых входа в систему, у каждого своя ответственность за авторизацию,
        // полагаться на то, что "юзер как-то уже провалидирован в другом месте", нельзя.
        if (!await _messages.IsMemberAsync(roomId, UserId))
            return Forbid();

        var messages = await _messages.GetMessagesAsync(roomId, beforeId, take);

        var response = messages.Select(m => new MessageResponse
        {
            Id = m.Id,
            RoomId = m.RoomId,
            SenderId = m.SenderId,
            SenderUserName = m.Sender.UserName,
            Content = m.Content,
            SentAt = m.SentAt,
            EditedAt = m.EditedAt
        }).ToList();

        return Ok(response);
    }
}