using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class modifydocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserDocuments_UserId_DocumentType",
                table: "UserDocuments");

            migrationBuilder.CreateIndex(
                name: "IX_UserDocuments_UserId_DocumentType",
                table: "UserDocuments",
                columns: new[] { "UserId", "DocumentType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserDocuments_UserId_DocumentType",
                table: "UserDocuments");

            migrationBuilder.CreateIndex(
                name: "IX_UserDocuments_UserId_DocumentType",
                table: "UserDocuments",
                columns: new[] { "UserId", "DocumentType" },
                unique: true);
        }
    }
}
