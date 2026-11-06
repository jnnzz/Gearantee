using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels
{
    public class NotificationItem
    {
        public long NotificationId { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string LinkUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }
    public class NotificationPage
    {
        public int Page { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / 10d));
        public List<NotificationItem> Items { get; set; } = new();
    }
}
