namespace ASI.Basecode.Data.Models
{
    public static class DomainValues
    {
        public static class Roles
        {
            public const string Borrower = "Borrower";
            public const string Custodian = "Custodian";
            public const string Administrator = "Administrator";
        }

        public static class Permissions
        {
            public const string EquipmentBrowse = "equipment.browse";
            public const string ReservationCreate = "reservation.create";
            public const string ReservationReview = "reservation.review";
            public const string TransactionReleaseReturn = "transaction.release_return";
            public const string EquipmentManage = "equipment.manage";
            public const string BorrowerManage = "borrower.manage";
            public const string UserRoleManage = "user_role.manage";
            public const string HistoryView = "history.view";
        }

        public static class ReservationStatuses
        {
            public const string Pending = "Pending";
            public const string Approved = "Approved";
            public const string Rejected = "Rejected";
            public const string Cancelled = "Cancelled";
            public const string Expired = "Expired";
        }

        public static class EquipmentStatuses
        {
            public const string Available = "Available";
            public const string Borrowed = "Borrowed";
            public const string UnderMaintenance = "UnderMaintenance";
            public const string Unavailable = "Unavailable";
            public const string Archived = "Archived";
        }
    }
}
