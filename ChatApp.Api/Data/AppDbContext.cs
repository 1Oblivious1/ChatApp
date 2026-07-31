using ChatApp.Api.DTOs;
using ChatApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ChatRoom> Rooms => Set<ChatRoom>();
    public DbSet<RoomMember> RoomMembers => Set<RoomMember>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---------- User ----------
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.UserName).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.UserName).HasMaxLength(32);
            e.Property(u => u.Email).HasMaxLength(256);
        });

        // ---------- ChatRoom ----------
        modelBuilder.Entity<ChatRoom>(e =>
        {
            e.Property(r => r.Name).HasMaxLength(128);
        });

        // ---------- RoomMember: составной ключ (RoomId, UserId) ----------
        modelBuilder.Entity<RoomMember>(e =>
        {
            e.HasKey(rm => new { rm.RoomId, rm.UserId });

            e.HasOne(rm => rm.Room)
                .WithMany(r => r.Members)
                .HasForeignKey(rm => rm.RoomId)
                .OnDelete(DeleteBehavior.Cascade); // удалили комнату -> удалились членства

            e.HasOne(rm => rm.User)
                .WithMany(u => u.RoomMembers)
                .HasForeignKey(rm => rm.UserId)
                .OnDelete(DeleteBehavior.Cascade); // удалили юзера -> удалились его членства

            // Обратный индекс: "во всех каких комнатах состоит юзер" —
            // отдельный индекс по UserId нужен, т.к. составной ключ (RoomId, UserId)
            // покрывает поиск "участники комнаты", но не "комнаты пользователя"
            e.HasIndex(rm => rm.UserId);
        });

        // ---------- Message ----------
        modelBuilder.Entity<Message>(e =>
        {
            e.Property(m => m.Id).UseIdentityAlwaysColumn(); // bigint identity в Postgres
            e.Property(m => m.Content).HasMaxLength(4000);

            // Основной паттерн чтения: "последние сообщения в комнате"
            e.HasIndex(m => new { m.RoomId, m.Id });

            e.HasOne(m => m.Room)
                .WithMany(r => r.Messages)
                .HasForeignKey(m => m.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            // Отправителя не каскадируем: если юзер удалён, сообщения должны остаться
            // (иначе история чата для остальных участников разрушится).
            // SetNull потребовал бы SenderId? — оставляем Restrict и решаем на уровне
            // приложения (soft-delete юзера), это осознанный компромисс.
            e.HasOne(m => m.Sender)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- RefreshToken ----------
        modelBuilder.Entity<RefreshToken>(e =>
        {
            // Поиск токена при обращении к /auth/refresh идёт по хэшу — нужен индекс,
            // иначе на каждый refresh будет full scan таблицы.
            e.HasIndex(rt => rt.TokenHash).IsUnique();

            // Для "выйти со всех устройств" — выборка всех активных токенов юзера
            e.HasIndex(rt => rt.UserId);

            e.HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade); // удалили юзера -> удалились его токены
        });
    }
}