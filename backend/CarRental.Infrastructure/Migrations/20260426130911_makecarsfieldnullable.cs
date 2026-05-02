using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class makecarsfieldnullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MonthlyRentalPayment_Cars_CarPlate",
                table: "MonthlyRentalPayment");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyRentalPayment_CarPlate",
                table: "MonthlyRentalPayment");

            migrationBuilder.DropIndex(
                name: "IX_Cars_ChassisNumber",
                table: "Cars");

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

            migrationBuilder.AlterColumn<int>(
                name: "Year",
                table: "Cars",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Model",
                table: "Cars",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ChassisNumber",
                table: "Cars",
                type: "nvarchar(17)",
                maxLength: 17,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(17)",
                oldMaxLength: 17);

            migrationBuilder.AlterColumn<string>(
                name: "Brand",
                table: "Cars",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyRentalPayment_CarPlate1",
                table: "MonthlyRentalPayment",
                column: "CarPlate1");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_ChassisNumber",
                table: "Cars",
                column: "ChassisNumber",
                unique: true,
                filter: "[ChassisNumber] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_MonthlyRentalPayment_Cars_CarPlate1",
                table: "MonthlyRentalPayment",
                column: "CarPlate1",
                principalTable: "Cars",
                principalColumn: "CarPlate",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MonthlyRentalPayment_Cars_CarPlate1",
                table: "MonthlyRentalPayment");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyRentalPayment_CarPlate1",
                table: "MonthlyRentalPayment");

            migrationBuilder.DropIndex(
                name: "IX_Cars_ChassisNumber",
                table: "Cars");

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

            migrationBuilder.AlterColumn<int>(
                name: "Year",
                table: "Cars",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Model",
                table: "Cars",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ChassisNumber",
                table: "Cars",
                type: "nvarchar(17)",
                maxLength: 17,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(17)",
                oldMaxLength: 17,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Brand",
                table: "Cars",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyRentalPayment_CarPlate",
                table: "MonthlyRentalPayment",
                column: "CarPlate");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_ChassisNumber",
                table: "Cars",
                column: "ChassisNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MonthlyRentalPayment_Cars_CarPlate",
                table: "MonthlyRentalPayment",
                column: "CarPlate",
                principalTable: "Cars",
                principalColumn: "CarPlate");
        }
    }
}
