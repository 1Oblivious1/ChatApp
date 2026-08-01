using System.ComponentModel.DataAnnotations;

namespace ChatApp.Api.DTOs;

public class CreateRoomRequest
{
    // Для приватного диалога (IsDirect = true) Name можно не указывать —
    // отображаемое имя строится на клиенте из имени собеседника
    [MaxLength(128)]
    public string? Name { get; set; }

    public bool IsDirect { get; set; }

    // Остальные участники, кроме создателя — его добавляем в комнату автоматически
    [Required, MinLength(1)]
    public List<Guid> MemberUserIds { get; set; } = new();
}