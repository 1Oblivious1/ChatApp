using System.ComponentModel.DataAnnotations;

namespace ChatApp.Api.DTOs;

public class LoginRequest
{
    [Required]
    public string UserNameOrEmail { get; set; } = default!;

    [Required]
    public string Password { get; set; } = default!;
}