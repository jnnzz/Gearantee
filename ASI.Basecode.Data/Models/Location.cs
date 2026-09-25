using System.Collections.Generic;

namespace ASI.Basecode.Data.Models
{
    public class Location
    {
        public long LocationId { get; set; }
        public string LocationName { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<EquipmentItem> EquipmentItems { get; set; } = new List<EquipmentItem>();
    }
}
