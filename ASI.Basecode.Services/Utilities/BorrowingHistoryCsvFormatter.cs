using ASI.Basecode.Services.ServiceModels.BorrowingHistory;
using System;
using System.Globalization;
using System.Linq;

namespace ASI.Basecode.Services.Utilities
{
    // WBS 55: converts shared history rows to escaped CSV cells;
    // BorrowingHistoryController streams these lines into the HTTP response.
    public static class BorrowingHistoryCsvFormatter
    {
        private static readonly string[] Headers =
        {
            "Reservation", "Loan", "Borrower", "User code", "School ID",
            "Item code", "Equipment", "Category", "Requested at (Manila)",
            "Borrowing starts (Manila)", "Due (Manila)", "Status",
            "Released at (Manila)", "Returned at (Manila)",
            "Returned condition", "Late return", "Days late"
        };

        public static string FormatHeader() =>
            string.Join(",", Headers.Select(EscapeCell));

        public static string FormatRow(BorrowingHistoryRowViewModel row)
        {
            // ★ WBS 55: keep the row values in the same 17-column order as the header
            // ★ and XLSX exporter. Missing lifecycle dates become empty CSV cells.
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            return string.Join(",", new[]
            {
                row.ReservationCode,
                row.LoanCode,
                row.BorrowerName,
                row.UserCode,
                row.SchoolId,
                row.ItemCode,
                row.ItemName,
                row.CategoryName,
                FormatDate(row.RequestedAtLocal),
                row.IsFullDay ? row.StartLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : FormatDate(row.StartLocal),
                row.IsFullDay ? row.DueLocal.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : FormatDate(row.DueLocal),
                row.Status,
                FormatNullableDate(row.ReleasedAtLocal),
                FormatNullableDate(row.ReturnedAtLocal),
                row.ReturnedCondition,
                row.HasLateReturn ? "Yes" : "No",
                row.DaysLate.ToString(CultureInfo.InvariantCulture)
            }.Select(EscapeCell));
        }

        public static string EscapeCell(string value)
        {
            // ★ User-entered text could be read as a spreadsheet formula. Prefix risky
            // ★ values, then quote every cell and double embedded quotes for valid CSV.
            value ??= string.Empty;
            var significantText = value.TrimStart();
            if (significantText.Length > 0 && "=+-@".Contains(significantText[0]))
            {
                value = "'" + value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string FormatDate(DateTime value) =>
            value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        private static string FormatNullableDate(DateTime? value) =>
            value.HasValue ? FormatDate(value.Value) : string.Empty;
    }
}
