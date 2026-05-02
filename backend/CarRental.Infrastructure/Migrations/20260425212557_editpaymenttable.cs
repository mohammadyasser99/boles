using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class editpaymenttable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MonthlyRentalPayment_Cars_CarPlate1",
                table: "MonthlyRentalPayment");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyRentalPayment_CarPlate1",
                table: "MonthlyRentalPayment");

            migrationBuilder.DropColumn(
                name: "CarPlate1",
                table: "MonthlyRentalPayment");

            migrationBuilder.AlterColumn<string>(
                name: "CarPlate",
                table: "MonthlyRentalPayment",
                type: "nvarchar(20)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyRentalPayment_CarPlate",
                table: "MonthlyRentalPayment",
                column: "CarPlate");

            migrationBuilder.AddForeignKey(
                name: "FK_MonthlyRentalPayment_Cars_CarPlate",
                table: "MonthlyRentalPayment",
                column: "CarPlate",
                principalTable: "Cars",
                principalColumn: "CarPlate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MonthlyRentalPayment_Cars_CarPlate",
                table: "MonthlyRentalPayment");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyRentalPayment_CarPlate",
                table: "MonthlyRentalPayment");

            migrationBuilder.AlterColumn<string>(
                name: "CarPlate",
                table: "MonthlyRentalPayment",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarPlate1",
                table: "MonthlyRentalPayment",
                type: "nvarchar(20)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyRentalPayment_CarPlate1",
                table: "MonthlyRentalPayment",
                column: "CarPlate1");

            migrationBuilder.AddForeignKey(
                name: "FK_MonthlyRentalPayment_Cars_CarPlate1",
                table: "MonthlyRentalPayment",
                column: "CarPlate1",
                principalTable: "Cars",
                principalColumn: "CarPlate",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
