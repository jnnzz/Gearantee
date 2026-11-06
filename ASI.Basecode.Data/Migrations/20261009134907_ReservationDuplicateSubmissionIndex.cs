using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASI.Basecode.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReservationDuplicateSubmissionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservation_BorrowerProfileId",
                table: "Reservation");

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_BorrowerProfileId_EquipmentId_Status_ReservationStart_ReservationEnd",
                table: "Reservation",
                columns: new[] { "BorrowerProfileId", "EquipmentId", "Status", "ReservationStart", "ReservationEnd" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservation_BorrowerProfileId_EquipmentId_Status_ReservationStart_ReservationEnd",
                table: "Reservation");

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_BorrowerProfileId",
                table: "Reservation",
                column: "BorrowerProfileId");
        }
    }
}
