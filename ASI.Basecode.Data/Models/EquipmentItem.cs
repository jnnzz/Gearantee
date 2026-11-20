using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models
{
    public class EquipmentItem
    {
        public long EquipmentId { get; set; }
        public long CategoryId { get; set; }
        public string Location { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public string Description { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public string ConditionStatus { get; set; }
        public string ItemStatus { get; set; }
        public string ImageUrl { get; set; }
        // WBS 28: soft-delete flag set by EquipmentItemService.SetArchivedAsync.
        // Keep the row and reservation links; inventory filters decide which list shows it.
        public bool IsArchived { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public EquipmentCategory Category { get; set; }
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}
