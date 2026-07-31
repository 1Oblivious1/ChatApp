using ChatApp.Api.Data;
using ChatApp.Api.DTOs;
using ChatApp.Api.Models;
using ChatApp.Api.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, ITokenService tokens, IConfiguration config) : ControllerBase
{
    private readonly AppDbContext _db = db;
    private readonly ITokenService _tokens = tokens;
    private readonly IConfiguration _config = config;

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var exists = await _db.Users.AnyAsync(u =>
            u.UserName == request.UserName || u.Email == request.Email);

        if (exists)
            return Conflict(new { message = "Пользователь с таким именем или email уже существует" });

        var user = new User
        {
            UserName = request.UserName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return await IssueTokensAsync(user);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.UserName == request.UserNameOrEmail || u.Email == request.UserNameOrEmail);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Неверные учётные данные" });

        return await IssueTokensAsync(user);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        var hash = _tokens.HashToken(request.RefreshToken);

        var stored = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash);

        if (stored is null)
            return Unauthorized(new { message = "Недействительный refresh-токен" });

        if (!stored.IsActive)
        {
            // Токен уже был отозван/использован, но кто-то пытается использовать его снова —
            // явный признак кражи токена. Реагируем максимально консервативно:
            // отзываем ВСЕ активные refresh-токены этого юзера, вынуждая перелогиниться
            // на всех устройствах.
            var allActive = await _db.RefreshTokens
                .Where(rt => rt.UserId == stored.UserId && rt.RevokedAt == null)
                .ToListAsync();

            foreach (var t in allActive)
                t.RevokedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Unauthorized(new { message = "Токен уже был использован, все сессии отозваны" });
        }

        // Ротация: старый токен отзываем, выдаём новый. Так каждый refresh-токен
        // используется ровно один раз — это и есть механизм детекта кражи выше.
        stored.RevokedAt = DateTime.UtcNow;

        var result = await IssueTokensAsync(stored.User);
        stored.ReplacedByTokenHash = _tokens.HashToken(result.Value!.RefreshToken);
        await _db.SaveChangesAsync();

        return result;
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        var hash = _tokens.HashToken(request.RefreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == hash);

        if (stored is not null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    private async Task<ActionResult<AuthResponse>> IssueTokensAsync(User user)
    {
        var accessToken = _tokens.GenerateAccessToken(user);
        var (rawRefresh, refreshHash) = _tokens.GenerateRefreshToken();

        var refreshDays = _config.GetValue("Jwt:RefreshTokenDays", 14);

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays)
        });

        await _db.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAt = _tokens.GetAccessTokenExpiry(),
            RefreshToken = rawRefresh,
            UserId = user.Id,
            UserName = user.UserName
        };
    }
}