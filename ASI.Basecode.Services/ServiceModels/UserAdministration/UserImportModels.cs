using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels.UserAdministration
{
    public sealed record UserImportError(int Row, string Field, string Message);
    public sealed record UserImportRow(int RowNumber, CreateUserViewModel Account);

    public sealed class ImportUsersViewModel
    {
        public IReadOnlyList<UserImportError> Errors { get; set; } = new List<UserImportError>();
    }

    public sealed class UserImportResult
    {
        public bool Forbidden { get; init; }
        public int CreatedCount { get; init; }
        public IReadOnlyList<UserImportError> Errors { get; init; } = new List<UserImportError>();
    }

    public sealed class UserCsvParseResult
    {
        public IReadOnlyList<UserImportRow> Rows { get; init; } = new List<UserImportRow>();
        public IReadOnlyList<UserImportError> Errors { get; init; } = new List<UserImportError>();
    }
}
