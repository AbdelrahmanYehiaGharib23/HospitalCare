using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalCare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentPendingDeactivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PendingDeactivation",
                table: "Departments",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingDeactivation",
                table: "Departments");
        }
    }
}
