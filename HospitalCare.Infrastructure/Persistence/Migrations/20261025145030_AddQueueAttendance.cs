using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalCare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQueueAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CalledAt",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckedInAt",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SkipCount",
                table: "Appointments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_SkipCount",
                table: "Appointments",
                sql: "[SkipCount] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_SkipCount",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "CalledAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "CheckedInAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "SkipCount",
                table: "Appointments");
        }
    }
}
