using System.ComponentModel.DataAnnotations;

namespace ChatApp.Api.DTOs;

public class RegisterRequest
{
    [Required, MinLength(3), MaxLength(32)]
    public string UserName { get; set; } = default!;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = default!;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = default!;
}
