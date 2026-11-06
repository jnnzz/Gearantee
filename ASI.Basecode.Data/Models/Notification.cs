using System;

namespace ASI.Basecode.Data.Models
{
    public class Notification
    {
        public long NotificationId { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string LinkUrl { get; set; }
        public long RelatedEntityId { get; set; }
        public string DedupKey { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool EmailRequested { get; set; }
        public string EmailStatus { get; set; }
        public int EmailAttempts { get; set; }
        public DateTime? EmailNextAttemptAt { get; set; }
        public DateTime? EmailSentAt { get; set; }
        public string EmailLastError { get; set; }
        public string EmailLeaseToken { get; set; }
        public DateTime? EmailLeaseExpiresAt { get; set; }
    }
}
