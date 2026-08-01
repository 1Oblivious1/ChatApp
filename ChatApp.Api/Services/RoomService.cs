using ChatApp.Api.Data;
using ChatApp.Api.Models;
using ChatApp.Api.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Api.Services;

public class RoomService : IRoomService
{
    private readonly AppDbContext _db;

    public RoomService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ChatRoom> CreateRoomAsync(Guid creatorId, string? name, bool isDirect, List<Guid> memberUserIds)
    {
        // Создатель всегда участник, даже если забыл/не стал явно указывать себя в списке
        var allMemberIds = memberUserIds.Append(creatorId).Distinct().ToList();

        var users = await _db.Users.Where(u => allMemberIds.Contains(u.Id)).ToListAsync();
        if (users.Count != allMemberIds.Count)
            throw new InvalidOperationException("Один или несколько указанных пользователей не существуют");

        if (isDirect && allMemberIds.Count != 2)
            throw new InvalidOperationException("Приватный диалог должен содержать ровно двух участников");

        var room = new ChatRoom
        {
            Name = name,
            IsDirect = isDirect
        };

        foreach (var user in users)
        {
            room.Members.Add(new RoomMember
            {
                UserId = user.Id,
                User = user, // важно: без этого навигационное свойство останется null после SaveChanges,
                             // а MapToResponse в контроллере обращается к m.User.UserName сразу же
                Role = user.Id == creatorId ? RoomRole.Admin : RoomRole.Member
            });
        }

        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();

        return room;
    }

    public async Task<List<ChatRoom>> GetUserRoomsAsync(Guid userId)
    {
        return await _db.Rooms
            .Where(r => r.Members.Any(m => m.UserId == userId))
            .Include(r => r.Members)
                .ThenInclude(m => m.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }
}