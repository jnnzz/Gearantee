using ASI.Basecode.Services.ServiceModels.BorrowingHistory;
using ASI.Basecode.Services.Utilities;
using Xunit;

namespace ASI.Basecode.Tests.BorrowingHistory
{
    public sealed class BorrowingHistoryCsvFormatterTests
    {
        [Fact]
        public void Full_day_export_shows_inclusive_end_date_and_preserves_actual_return_time()
        {
            var row = new BorrowingHistoryRowViewModel {
                IsFullDay = true,
                StartLocal = new System.DateTime(2026, 11, 7),
                DueLocal = new System.DateTime(2026, 11, 10),
                ReturnedAtLocal = new System.DateTime(2026, 11, 9, 16, 30, 0)
            };
            var csv = BorrowingHistoryCsvFormatter.FormatRow(row);
            Assert.Contains("\"2026-11-07\",\"2026-11-09\"", csv);
            Assert.Contains("\"2026-11-09 16:30\"", csv);
        }

        [Fact]
        public void Csv_fields_are_quoted_and_formula_like_borrower_text_is_neutralized()
        {
            var csv = BorrowingHistoryCsvFormatter.FormatRow(
                new BorrowingHistoryRowViewModel
                {
                    ReservationId = 12,
                    BorrowerName = "=HYPERLINK(\"https://example.test\")",
                    UserCode = "STU-12",
                    SchoolId = "2026-12",
                    ItemCode = "CAM-1",
                    ItemName = "Camera, DSLR",
                    CategoryName = "Cameras",
                    Status = "Returned"
                });

            Assert.Contains("\"'=HYPERLINK(\"\"https://example.test\"\")\"", csv);
            Assert.Contains("\"Camera, DSLR\"", csv);
            Assert.StartsWith("\"RSV-12\",", csv);
        }
    }
}
