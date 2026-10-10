using ASI.Basecode.Services.ServiceModels.UserAdministration;
using ASI.Basecode.Services.Services;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.UserAdministration
{
    public class UserImportCsvTests
    {
        [Fact]
        public async Task ParsesUtf8BomQuotedFieldsAndPreservesCodesAndPasswordWhitespace()
        {
            var csv = "\uFEFF" + UserImportCsv.Header + "\r\n001,\"Ana, Maria\",\"Dela \"\"Cruz\"\"\",ana@example.test,Borrower, Password!123 ,0001,\"Arts\nStudies\",\r\n";
            var result = await Parse(csv);
            Assert.Empty(result.Errors);
            var row = Assert.Single(result.Rows);
            Assert.Equal(2, row.RowNumber);
            Assert.Equal("001", row.Account.UserCode);
            Assert.Equal("0001", row.Account.SchoolId);
            Assert.Equal("Ana, Maria", row.Account.FirstName);
            Assert.Equal("Dela \"Cruz\"", row.Account.LastName);
            Assert.Equal("Arts\nStudies", row.Account.Department);
            Assert.Equal(" Password!123 ", row.Account.Password);
        }

        [Theory]
        [InlineData("")]
        [InlineData(UserImportCsv.Header)]
        [InlineData("UserCode,FirstName,LastName,Email,RoleName,Password,SchoolId,Department,Email\n")]
        [InlineData("UserCode,FirstName,LastName,Email,RoleName,Password,SchoolId,Department,Unknown\n")]
        [InlineData(UserImportCsv.Header + "\n001,A,B,a@test.local,Borrower,Secret!123\n")]
        [InlineData(UserImportCsv.Header + "\n001,A,B,a@test.local,Borrower,\"Secret!123\n")]
        public async Task InvalidFilesReturnSafeErrorsAndNoRows(string csv)
        {
            var result = await Parse(csv);
            Assert.NotEmpty(result.Errors);
            Assert.Empty(result.Rows);
            Assert.DoesNotContain("Secret!123", JsonSerializer.Serialize(result.Errors));
        }

        [Fact]
        public async Task RejectsOversizeTooManyRowsAndNonUtf8()
        {
            Assert.NotEmpty((await Parse(new string('a', UserImportCsv.MaxFileBytes + 1))).Errors);
            var rows = Enumerable.Range(1, 101).Select(i => $"{i},A,B,u{i}@test.local,Custodian,Secret!123,,,");
            Assert.NotEmpty((await Parse(UserImportCsv.Header + "\n" + string.Join("\n", rows))).Errors);
            using var stream = new MemoryStream(new byte[] { 0xff, 0xfe, 0x00 });
            Assert.NotEmpty((await UserImportCsv.ParseAsync(stream)).Errors);
        }

        internal static async Task<UserCsvParseResult> Parse(string csv)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            return await UserImportCsv.ParseAsync(stream);
        }
    }
}
