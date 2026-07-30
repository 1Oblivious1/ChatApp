namespace ChatApp.Api.Models
{
    public class RoomMember
    {
        public Guid RoomId { get; set; }
        public Guid UserId { get; set; }
        public DateTime JoinedAt { get; set; }
        public DateTime? LastReadAt { get; set; } // для "прочитано"
    }
}