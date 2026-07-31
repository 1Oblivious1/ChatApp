using ChatApp.Api.Data;
using ChatApp.Api.Models;
using ChatApp.Api.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Api.Services;

public class MessageService : IMessageService
{
    private readonly AppDbContext _db;

    public MessageService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Guid>> GetUserRoomIdsAsync(Guid userId)
    {
        return await _db.RoomMembers
            .Where(rm => rm.UserId == userId)
            .Select(rm => rm.RoomId)
            .ToListAsync();
    }

    public async Task<bool> IsMemberAsync(Guid roomId, Guid userId)
    {
        // AnyAsync по составному PK (RoomId, UserId) — используется индекс первичного ключа,
        // дешёвый point-lookup, не сканирование.
        return await _db.RoomMembers.AnyAsync(rm => rm.RoomId == roomId && rm.UserId == userId);
    }

    public async Task<Message> SaveMessageAsync(Guid roomId, Guid senderId, string content)
    {
        var message = new Message
        {
            RoomId = roomId,
            SenderId = senderId,
            Content = content,
            SentAt = DateTime.UtcNow // время сервера, не доверяем клиенту
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

        return message;
    }
}