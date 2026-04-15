using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace platform_ai_backend_netcore.Domain.Entities
{
    [Table("users")]
    public class User
    {
        [Column("id")]
        public Guid Id { get; set; }

        [Column("firebase_uid")]
        public string FirebaseUid { get; set; } = string.Empty;

        [Column("email")]
        public string? Email { get; set; }

        [Column("full_name")]
        public string? FullName { get; set; }

        [Column("avatar_url")]
        public string? AvatarUrl { get; set; }

        [Column("status")]
        public string Status { get; set; } = "active";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
        public List<UserSession> Sessions { get; set; } = [];
    }
}