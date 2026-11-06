using ASI.Basecode.Services.ServiceModels.BorrowingHistory;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Utilities
{
    /// <summary>Writes the all-matching borrowing history stream into a bounded-memory XLSX file.</summary>
    public static class BorrowingHistoryXlsxExporter
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        public const uint MaxWorksheetRows = 1_048_576;

        private static readonly string[] Headers =
        {
            "Reservation", "Loan", "Borrower", "User code", "School ID",
            "Item code", "Equipment", "Category", "Requested at (Manila)",
            "Borrowing starts (Manila)", "Due (Manila)", "Status",
            "Released at (Manila)", "Returned at (Manila)",
            "Returned condition", "Late return", "Days late"
        };

        private static readonly double[] ColumnWidths =
        {
            18, 14, 30, 18, 18, 18, 42, 24, 24, 24, 24, 18, 24, 24, 24, 15, 12
        };

        public static async Task<FileStream> CreateAsync(
            IAsyncEnumerable<BorrowingHistoryRowViewModel> rows,
            CancellationToken cancellationToken = default)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var path = Path.Combine(
                Path.GetTempPath(),
                $"gearantee-borrowing-history-{Guid.NewGuid():N}.xlsx");
            try
            {
                using (var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
                {
                    var workbookPart = document.AddWorkbookPart();
                    workbookPart.Workbook = new Workbook();
                    AddStyles(workbookPart);

                    var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                    using (var writer = OpenXmlWriter.Create(worksheetPart))
                    {
                        writer.WriteStartElement(new Worksheet());
                        WriteFrozenHeader(writer);
                        WriteColumns(writer);
                        writer.WriteStartElement(new SheetData());

                        WriteRow(writer, 1, Headers, header: true);
                        uint rowIndex = 2;
                        await foreach (var row in rows.WithCancellation(cancellationToken))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (rowIndex > MaxWorksheetRows)
                            {
                                throw new BorrowingHistoryWorkbookLimitException(
                                    $"An Excel worksheet can contain at most {MaxWorksheetRows - 1:N0} report records.");
                            }

                            WriteReportRow(writer, rowIndex, row);
                            rowIndex++;
                        }

                        writer.WriteEndElement(); // SheetData
                        writer.WriteEndElement(); // Worksheet
                    }

                    workbookPart.Workbook.AppendChild(new Sheets(
                        new Sheet
                        {
                            Id = workbookPart.GetIdOfPart(worksheetPart),
                            SheetId = 1,
                            Name = "Borrowing history"
                        }));
                    workbookPart.Workbook.Save();
                }

                return new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read | FileShare.Delete,
                    bufferSize: 81920,
                    options: FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            }
            catch
            {
                TryDelete(path);
                throw;
            }
        }

        private static void AddStyles(WorkbookPart workbookPart)
        {
            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = new Stylesheet(
                new NumberingFormats(
                    new NumberingFormat
                    {
                        NumberFormatId = 164,
                        FormatCode = "yyyy-mm-dd hh:mm"
                    }) { Count = 1 },
                new Fonts(
                    new Font(),
                    new Font(new Bold())) { Count = 2 },
                new Fills(
                    new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 })) { Count = 2 },
                new Borders(new Border()) { Count = 1 },
                new CellStyleFormats(new CellFormat()) { Count = 1 },
                new CellFormats(
                    new CellFormat(),
                    new CellFormat { FontId = 1, ApplyFont = true },
                    new CellFormat { NumberFormatId = 164, ApplyNumberFormat = true },
                    new CellFormat
                    {
                        Alignment = new Alignment
                        {
                            WrapText = true,
                            Vertical = VerticalAlignmentValues.Top
                        },
                        ApplyAlignment = true
                    }) { Count = 4 },
                new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }));
            stylesPart.Stylesheet.Save();
        }

        private static void WriteFrozenHeader(OpenXmlWriter writer)
        {
            writer.WriteStartElement(new SheetViews());
            writer.WriteStartElement(new SheetView { WorkbookViewId = 0 });
            writer.WriteElement(new Pane
            {
                VerticalSplit = 1,
                TopLeftCell = "A2",
                ActivePane = PaneValues.BottomLeft,
                State = PaneStateValues.Frozen
            });
            writer.WriteEndElement(); // SheetView
            writer.WriteEndElement(); // SheetViews
        }

        private static void WriteColumns(OpenXmlWriter writer)
        {
            writer.WriteStartElement(new Columns());
            for (var index = 0; index < ColumnWidths.Length; index++)
            {
                var column = (uint)index + 1;
                writer.WriteElement(new Column
                {
                    Min = column,
                    Max = column,
                    Width = ColumnWidths[index],
                    CustomWidth = true
                });
            }
            writer.WriteEndElement(); // Columns
        }

        private static void WriteReportRow(
            OpenXmlWriter writer,
            uint rowIndex,
            BorrowingHistoryRowViewModel row)
        {
            if (row == null) throw new InvalidDataException("The borrowing history stream returned a null row.");

            var texts = new[]
            {
                row.ReservationCode,
                row.LoanCode,
                row.BorrowerName,
                row.UserCode,
                row.SchoolId,
                row.ItemCode,
                row.ItemName,
                row.CategoryName,
                null,
                null,
                null,
                row.Status,
                null,
                null,
                row.ReturnedCondition,
                row.HasLateReturn ? "Yes" : "No",
                null
            };

            writer.WriteStartElement(new Row { RowIndex = rowIndex });
            for (var index = 0; index < texts.Length; index++)
            {
                var cell = (uint)index + 1;
                if (index is 8 or 9 or 10 or 12 or 13)
                {
                    var date = index switch
                    {
                        8 => row.RequestedAtLocal,
                        9 => row.StartLocal,
                        10 => row.IsFullDay ? row.DueLocal.AddDays(-1) : row.DueLocal,
                        12 => row.ReleasedAtLocal,
                        _ => row.ReturnedAtLocal
                    };
                    WriteDateCell(writer, rowIndex, cell, date);
                }
                else if (index == 16)
                {
                    WriteNumberCell(writer, rowIndex, cell, row.DaysLate);
                }
                else
                {
                    WriteTextCell(writer, rowIndex, cell, texts[index],
                        wrap: index is 2 or 6 or 7 or 14);
                }
            }
            writer.WriteEndElement(); // Row
        }

        private static void WriteRow(OpenXmlWriter writer, uint rowIndex, string[] values, bool header)
        {
            writer.WriteStartElement(new Row { RowIndex = rowIndex });
            for (var index = 0; index < values.Length; index++)
            {
                var cell = new Cell
                {
                    CellReference = CellReference((uint)index + 1, rowIndex),
                    DataType = CellValues.InlineString,
                    StyleIndex = header ? 1U : 0U,
                    InlineString = new InlineString(new Text(values[index] ?? string.Empty)
                    {
                        Space = SpaceProcessingModeValues.Preserve
                    })
                };
                writer.WriteElement(cell);
            }
            writer.WriteEndElement(); // Row
        }

        private static void WriteTextCell(
            OpenXmlWriter writer,
            uint row,
            uint column,
            string value,
            bool wrap)
        {
            writer.WriteElement(new Cell
            {
                CellReference = CellReference(column, row),
                DataType = CellValues.InlineString,
                StyleIndex = wrap ? 3U : 0U,
                InlineString = new InlineString(new Text(value ?? string.Empty)
                {
                    Space = SpaceProcessingModeValues.Preserve
                })
            });
        }

        private static void WriteDateCell(OpenXmlWriter writer, uint row, uint column, DateTime? value)
        {
            var cell = new Cell
            {
                CellReference = CellReference(column, row),
                StyleIndex = 2
            };
            if (value.HasValue)
            {
                cell.DataType = CellValues.Number;
                cell.CellValue = new CellValue(value.Value.ToOADate().ToString(CultureInfo.InvariantCulture));
            }
            writer.WriteElement(cell);
        }

        private static void WriteNumberCell(OpenXmlWriter writer, uint row, uint column, int value)
        {
            writer.WriteElement(new Cell
            {
                CellReference = CellReference(column, row),
                DataType = CellValues.Number,
                CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture))
            });
        }

        private static string CellReference(uint column, uint row)
        {
            var letters = string.Empty;
            while (column > 0)
            {
                var remainder = (column - 1) % 26;
                letters = (char)('A' + remainder) + letters;
                column = (column - 1) / 26;
            }
            return letters + row.ToString(CultureInfo.InvariantCulture);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup after an export failure; never mask the original exception.
            }
        }
    }

    public sealed class BorrowingHistoryWorkbookLimitException : Exception
    {
        public BorrowingHistoryWorkbookLimitException(string message) : base(message) { }
    }
}
