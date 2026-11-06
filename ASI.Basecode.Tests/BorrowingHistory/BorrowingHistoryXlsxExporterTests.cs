using ASI.Basecode.Services.ServiceModels.BorrowingHistory;
using ASI.Basecode.Services.Utilities;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.BorrowingHistory
{
    public sealed class BorrowingHistoryXlsxExporterTests
    {
        [Fact]
        public async Task Workbook_keeps_text_literal_dates_numeric_widths_and_all_report_rows()
        {
            var rows = Enumerable.Range(1, 23).Select(index => new BorrowingHistoryRowViewModel
            {
                ReservationId = index,
                LoanCode = index == 1 ? null : $"LN-{index:000}",
                BorrowerName = index == 1 ? "Renée O'Neil" : $"Borrower {index}",
                UserCode = "000712",
                SchoolId = "0012345",
                ItemCode = "CAM-001",
                ItemName = index == 1 ? "=1+1, camera\nwith line break" : $"Camera {index}",
                CategoryName = "Cameras",
                Status = "Returned",
                RequestedAtLocal = new DateTime(2026, 10, 14, 9, 0, 0),
                StartLocal = new DateTime(2026, 10, 14, 10, 0, 0),
                DueLocal = new DateTime(2026, 10, 14, 12, 0, 0),
                ReleasedAtLocal = new DateTime(2026, 10, 14, 10, 2, 0),
                ReturnedAtLocal = index == 1 ? null : new DateTime(2026, 10, 14, 13, 0, 0),
                ReturnedCondition = "Good",
                HasLateReturn = index != 1,
                DaysLate = index == 1 ? 0 : 1
            }).ToArray();

            await using var stream = await BorrowingHistoryXlsxExporter.CreateAsync(ToAsync(rows));
            using var workbook = SpreadsheetDocument.Open(stream, false);
            var workbookPart = workbook.WorkbookPart;
            var sheet = Assert.Single(workbookPart.Workbook.Sheets.Elements<Sheet>());
            Assert.Equal("Borrowing history", sheet.Name.Value);
            var worksheet = workbookPart.GetPartById(sheet.Id.Value) as WorksheetPart;
            Assert.NotNull(worksheet);
            var sheetData = Assert.Single(worksheet.Worksheet.Elements<SheetData>());
            var savedRows = sheetData.Elements<Row>().ToArray();

            Assert.Equal(24, savedRows.Length); // Header plus all 23, not just a page.
            Assert.Equal(17, savedRows[0].Elements<Cell>().Count());
            Assert.Equal("Reservation", savedRows[0].Elements<Cell>().First().InlineString.InnerText);
            Assert.Equal("RSV-1", InlineText(savedRows[1], "A2"));
            Assert.Equal("000712", InlineText(savedRows[1], "D2"));
            Assert.Equal("0012345", InlineText(savedRows[1], "E2"));
            Assert.Equal("=1+1, camera\nwith line break", InlineText(savedRows[1], "G2"));
            Assert.Null(savedRows[1].Elements<Cell>().Single(cell => cell.CellReference == "G2").CellFormula);
            Assert.Equal("000712", InlineText(savedRows[1], "D2"));
            Assert.Equal(string.Empty, savedRows[1].Elements<Cell>().Single(cell => cell.CellReference == "N2").CellValue?.Text ?? string.Empty);

            var dateCell = savedRows[1].Elements<Cell>().Single(cell => cell.CellReference == "I2");
            Assert.Equal(CellValues.Number, dateCell.DataType.Value);
            Assert.Equal(2U, dateCell.StyleIndex.Value);
            Assert.Equal(new DateTime(2026, 10, 14, 9, 0, 0).ToOADate().ToString(CultureInfo.InvariantCulture), dateCell.CellValue.Text);
            Assert.Equal(CellValues.Number, savedRows[1].Elements<Cell>().Single(cell => cell.CellReference == "Q2").DataType.Value);

            var dateFormat = workbookPart.WorkbookStylesPart.Stylesheet.CellFormats
                .Elements<CellFormat>().ElementAt(2);
            Assert.Equal(164U, dateFormat.NumberFormatId.Value);
            var columns = Assert.Single(worksheet.Worksheet.Elements<Columns>()).Elements<Column>().ToArray();
            Assert.Equal(17, columns.Length);
            Assert.All(columns, column => Assert.True(column.Width.Value >= 12));
            var pane = Assert.Single(worksheet.Worksheet.Elements<SheetViews>())
                .Elements<SheetView>().Single().Elements<Pane>().Single();
            Assert.Equal(PaneStateValues.Frozen, pane.State.Value);
            Assert.Equal("A2", pane.TopLeftCell.Value);
            Assert.All(worksheet.Worksheet.Descendants<Cell>(), cell => Assert.Null(cell.CellFormula));
            Assert.Null(workbookPart.ExternalWorkbookParts.FirstOrDefault());
            Assert.Empty(new OpenXmlValidator().Validate(workbook));
        }

        [Fact]
        public async Task Empty_exports_keep_the_header_and_cancelled_exports_remove_the_temporary_file()
        {
            await using (var empty = await BorrowingHistoryXlsxExporter.CreateAsync(
                ToAsync(Array.Empty<BorrowingHistoryRowViewModel>())))
            using (var workbook = SpreadsheetDocument.Open(empty, false))
            {
                var sheet = Assert.Single(workbook.WorkbookPart.Workbook.Sheets.Elements<Sheet>());
                var worksheet = (WorksheetPart)workbook.WorkbookPart.GetPartById(sheet.Id.Value);
                var rows = worksheet.Worksheet.Elements<SheetData>().Single().Elements<Row>().ToArray();
                Assert.Single(rows);
                Assert.Equal(17, rows[0].Elements<Cell>().Count());
            }

            var filesBefore = Directory.GetFiles(
                Path.GetTempPath(),
                "gearantee-borrowing-history-*.xlsx");
            using var cancellation = new System.Threading.CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await BorrowingHistoryXlsxExporter.CreateAsync(
                    ToAsync(new[] { new BorrowingHistoryRowViewModel() }),
                    cancellation.Token));

            var newFiles = Directory.GetFiles(
                    Path.GetTempPath(),
                    "gearantee-borrowing-history-*.xlsx")
                .Except(filesBefore);
            Assert.Empty(newFiles);
        }

        private static string InlineText(Row row, string reference) =>
            row.Elements<Cell>().Single(cell => cell.CellReference == reference).InlineString.InnerText;

        private static async IAsyncEnumerable<BorrowingHistoryRowViewModel> ToAsync(
            IEnumerable<BorrowingHistoryRowViewModel> rows)
        {
            foreach (var row in rows)
            {
                yield return row;
                await Task.Yield();
            }
        }
    }
}
