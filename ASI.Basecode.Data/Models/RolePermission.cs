using Microsoft.AspNetCore.Identity;

namespace ASI.Basecode.Data.Models
{
    public class RolePermission
    {
        public string RoleId { get; set; }
        public long PermissionId { get; set; }

        public IdentityRole Role { get; set; }
        public Permission Permission { get; set; }
    }
}
