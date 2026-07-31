using System.ComponentModel.DataAnnotations;

namespace ChatApp.Api.DTOs;

public class AuthResponse
{
    public string AccessToken { get; set; } = default!;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = default!;
    public Guid UserId { get; set; }
    public string UserName { get; set; } = default!;
}

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = default!;
}