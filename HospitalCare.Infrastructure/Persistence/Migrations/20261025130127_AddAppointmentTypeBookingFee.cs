using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalCare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentTypeBookingFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BookingFee",
                table: "AppointmentTypes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [AppointmentTypes] SET [BookingFee] = CASE " +
                "WHEN [Name] = N'Follow-up' THEN 50 ELSE 100 END " +
                "WHERE [BookingFee] IS NULL;");

            migrationBuilder.AlterColumn<decimal>(
                name: "BookingFee",
                table: "AppointmentTypes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppointmentTypes_BookingFee",
                table: "AppointmentTypes",
                sql: "[BookingFee] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AppointmentTypes_BookingFee",
                table: "AppointmentTypes");

            migrationBuilder.DropColumn(
                name: "BookingFee",
                table: "AppointmentTypes");
        }
    }
}
