using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models
{
    public class EquipmentCategory
    {
        public long CategoryId { get; set; }
        public string CategoryCode { get; set; }
        public string CategoryName { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<EquipmentItem> EquipmentItems { get; set; } = new List<EquipmentItem>();
    }
}
