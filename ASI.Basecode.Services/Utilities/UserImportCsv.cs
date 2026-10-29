using ASI.Basecode.Services.ServiceModels.UserAdministration;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public static class UserImportCsv
    {
        public const int MaxRows = 100;
        public const int MaxFileBytes = 1024 * 1024;
        public const int MaxRequestBytes = MaxFileBytes + 65536;
        public const string Header = "UserCode,FirstName,LastName,Email,RoleName,Password,SchoolId,Department,ContactNumber";

        public static async Task<UserCsvParseResult> ParseAsync(
            Stream input, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await input.ReadAsync(chunk.AsMemory(), cancellationToken)) > 0)
            {
                if (buffer.Length + read > MaxFileBytes)
                    return Failure(0, "File", "The CSV must be no larger than 1 MiB.");
                buffer.Write(chunk, 0, read);
            }

            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(buffer.ToArray()).TrimStart('\uFEFF');
            }
            catch (DecoderFallbackException)
            {
                return Failure(0, "File", "Save the file as UTF-8 CSV.");
            }

            using var parser = new TextFieldParser(new StringReader(text))
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = false
            };
            parser.SetDelimiters(",");
            var rowNumber = 1;
            try
            {
                if (parser.EndOfData)
                    return Failure(0, "File", "Choose a CSV containing a header and at least one account.");
                var header = parser.ReadFields().Select(value => value.Trim()).ToArray();
                var expected = Header.Split(',');
                if (header.Length != expected.Length ||
                    header.Distinct(StringComparer.OrdinalIgnoreCase).Count() != expected.Length ||
                    expected.Any(name => !header.Contains(name, StringComparer.OrdinalIgnoreCase)))
                    return Failure(1, "Header", "Use all nine template headers exactly once, with no extra columns.");

                var indexes = expected.ToDictionary(name => name,
                    name => Array.FindIndex(header, item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase)));
                var rows = new List<UserImportRow>();
                var errors = new List<UserImportError>();
                while (!parser.EndOfData)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    rowNumber = checked((int)parser.LineNumber);
                    if (rows.Count + errors.Count >= MaxRows)
                        return Failure(rowNumber, "File", "Import at most 100 accounts per file.");
                    var fields = parser.ReadFields();
                    if (fields.Length != expected.Length)
                    {
                        errors.Add(new UserImportError(rowNumber, "Row", "Expected nine columns. Quote fields that contain commas."));
                        continue;
                    }
                    string Value(string name) => fields[indexes[name]].Trim();
                    // Passwords are never trimmed, displayed, logged, or written to disk.
                    var password = fields[indexes["Password"]];
                    rows.Add(new UserImportRow(rowNumber, new CreateUserViewModel
                    {
                        UserCode = Value("UserCode"), FirstName = Value("FirstName"),
                        LastName = Value("LastName"), Email = Value("Email"),
                        RoleName = Value("RoleName"), Password = password, ConfirmPassword = password,
                        SchoolId = Value("SchoolId"), Department = Value("Department"),
                        ContactNumber = string.IsNullOrWhiteSpace(Value("ContactNumber")) ? null : Value("ContactNumber")
                    }));
                }
                if (errors.Count > 0) return new UserCsvParseResult { Errors = errors };
                return rows.Count == 0
                    ? Failure(0, "File", "Add at least one account below the header.")
                    : new UserCsvParseResult { Rows = rows };
            }
            catch (MalformedLineException)
            {
                // The exception and ErrorLine can contain passwords. Never return or log them.
                return Failure(rowNumber, "Row", "Malformed CSV quoting. Use the template and quote embedded commas correctly.");
            }
        }

        private static UserCsvParseResult Failure(int row, string field, string message) =>
            new UserCsvParseResult { Errors = new[] { new UserImportError(row, field, message) } };
    }
}
