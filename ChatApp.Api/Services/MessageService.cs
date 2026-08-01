using ChatApp.Api.Data;
using ChatApp.Api.Models;
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

    public async Task<List<Message>> GetMessagesAsync(Guid roomId, long? beforeId, int take)
    {
        // Ограничиваем take жёстко на сервере — иначе клиент мог бы запросить take=1000000
        // и создать неожиданную нагрузку на БД и сеть
        take = Math.Clamp(take, 1, 100);

        var query = _db.Messages
            .Include(m => m.Sender)
            .Where(m => m.RoomId == roomId && !m.IsDeleted);

        if (beforeId is not null)
            query = query.Where(m => m.Id < beforeId);

        // DESC по Id — индекс (RoomId, Id) полностью покрывает и фильтр, и сортировку,
        // отдельного шага сортировки в плане запроса не будет
        return await query
            .OrderByDescending(m => m.Id)
            .Take(take)
            .ToListAsync();
    }
}