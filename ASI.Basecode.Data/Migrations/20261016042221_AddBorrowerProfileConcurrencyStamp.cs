using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASI.Basecode.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBorrowerProfileConcurrencyStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                table: "BorrowerProfile",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
                // Existing rows would all share the empty default, which would make the
                // stale-edit check meaningless; give each one its own stamp.
                migrationBuilder.Sql(
                    "UPDATE BorrowerProfile SET ConcurrencyStamp = CONVERT(nvarchar(64), NEWID()) WHERE ConcurrencyStamp = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "BorrowerProfile");
        }
    }
}
