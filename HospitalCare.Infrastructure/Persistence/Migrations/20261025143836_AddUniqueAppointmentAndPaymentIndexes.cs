using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalCare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueAppointmentAndPaymentIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_GatewayReference",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_AppointmentDate_QueuePosition",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_GatewayReference",
                table: "PaymentTransactions",
                column: "GatewayReference",
                unique: true,
                filter: "[GatewayReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_AppointmentDate_QueuePosition",
                table: "Appointments",
                columns: new[] { "DoctorId", "AppointmentDate", "QueuePosition" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_GatewayReference",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_AppointmentDate_QueuePosition",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_GatewayReference",
                table: "PaymentTransactions",
                column: "GatewayReference");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_AppointmentDate_QueuePosition",
                table: "Appointments",
                columns: new[] { "DoctorId", "AppointmentDate", "QueuePosition" });
        }
    }
}
