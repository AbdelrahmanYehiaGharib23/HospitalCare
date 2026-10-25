using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalCare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymobTransactionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ProviderTransactionId",
                table: "PaymentTransactions",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderTransactionId",
                table: "PaymentTransactions");
        }
    }
}
