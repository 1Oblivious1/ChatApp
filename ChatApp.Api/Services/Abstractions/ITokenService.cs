using ChatApp.Api.Models;

namespace ChatApp.Api.Services.Abstractions;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    DateTime GetAccessTokenExpiry();

    /// <summary>Возвращает "сырой" refresh-токен для отдачи клиенту, и его хэш для сохранения в БД.</summary>
    (string RawToken, string Hash) GenerateRefreshToken();
    string HashToken(string rawToken);
}