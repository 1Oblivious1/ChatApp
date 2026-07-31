using ChatApp.Api.Models;

namespace ChatApp.Api.DTOs;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    // Храним SHA-256 хэш токена, а не сам токен — так же, как с паролями:
    // если БД утечёт, злоумышленник не получит готовые к использованию токены.
    public string TokenHash { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    // Отзыв (logout, ротация при refresh, компрометация)
    public DateTime? RevokedAt { get; set; }

    // При ротации — на какой токен заменён (для отслеживания цепочки/детекта повторного использования)
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}