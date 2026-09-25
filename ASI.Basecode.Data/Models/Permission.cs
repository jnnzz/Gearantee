using System.Collections.Generic;

namespace ASI.Basecode.Data.Models
{
    public class Permission
    {
        public long PermissionId { get; set; }
        public string PermissionName { get; set; }
        public string Description { get; set; }

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
