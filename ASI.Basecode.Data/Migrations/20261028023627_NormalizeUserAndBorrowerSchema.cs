using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASI.Basecode.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeUserAndBorrowerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            // RolePermissionSeed is deliberately retained: it records which roles have already
            // been initialized, so dropping it would make a later --seed run restore grants an
            // administrator intentionally removed (including roles left with zero grants).

            // SchoolId is replaced by the account identifier (AspNetUsers.UserName, exposed as
            // IdNumber). Dropping the column is only lossless when every borrower's SchoolId
            // already matches the linked account. If any differ (or a profile has no account),
            // stop and let an operator reconcile the values instead of silently deleting them.
            migrationBuilder.Sql(
                "IF EXISTS (" +
                "    SELECT 1 FROM [BorrowerProfile] AS profile " +
                "    LEFT JOIN [AspNetUsers] AS account ON account.[Id] = profile.[UserId] " +
                "    WHERE account.[Id] IS NULL OR profile.[SchoolId] <> account.[UserName]) " +
                "BEGIN " +
                "    THROW 51000, 'NormalizeUserAndBorrowerSchema stopped: one or more BorrowerProfile.SchoolId values differ from the linked account identifier (AspNetUsers.UserName), or a profile has no linked account. Dropping SchoolId would lose data. Inspect and reconcile those rows first, for example: SELECT profile.BorrowerProfileId, profile.SchoolId, account.UserName FROM BorrowerProfile AS profile LEFT JOIN AspNetUsers AS account ON account.Id = profile.UserId WHERE account.Id IS NULL OR profile.SchoolId <> account.UserName;', 1; " +
                "END;");

            migrationBuilder.DropIndex(
                name: "IX_BorrowerProfile_SchoolId",
                table: "BorrowerProfile");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "BorrowerProfile");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SchoolId",
                table: "BorrowerProfile",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // The original values were dropped by Up(), so restore a distinct, meaningful
            // identifier for every borrower before the unique index is recreated. The linked
            // account identifier (UserName) is unique per profile, so it satisfies the index.
            migrationBuilder.Sql(
                "UPDATE profile " +
                "SET profile.SchoolId = account.UserName " +
                "FROM BorrowerProfile AS profile " +
                "JOIN AspNetUsers AS account ON account.Id = profile.UserId;");

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BorrowerProfile_SchoolId",
                table: "BorrowerProfile",
                column: "SchoolId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");
        }
    }
}
