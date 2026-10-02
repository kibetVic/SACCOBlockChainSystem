// Models/UserSession.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SACCOBlockChainSystem.Models
{
    [Table("UserSessions")]
    public class UserSession
    {
        [Key]
        public long SessionId { get; set; }

        [Required]
        [StringLength(50)]
        public string UserId { get; set; } = string.Empty;

        [StringLength(150)]
        public string? UserName { get; set; }

        [StringLength(50)]
        public string? CompanyCode { get; set; }

        [StringLength(100)]
        public string? CompanyName { get; set; }

        [StringLength(50)]
        public string? UserGroup { get; set; }

        [StringLength(45)]
        public string? IpAddress { get; set; }

        public DateTime? LoginTime { get; set; }

        public DateTime? LogoutTime { get; set; }

        public DateTime? LastActivityTime { get; set; }

        [StringLength(500)]
        public string? SessionId_ { get; set; }

        [StringLength(500)]
        public string? BrowserAgent { get; set; }

        public bool IsActive { get; set; }

        [StringLength(100)]
        public string? SessionCookie { get; set; }

        public int? SessionDurationSeconds { get; set; }

        [StringLength(50)]
        public string? LogoutReason { get; set; } 

        [StringLength(100)]
        public string? DeviceInfo { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}