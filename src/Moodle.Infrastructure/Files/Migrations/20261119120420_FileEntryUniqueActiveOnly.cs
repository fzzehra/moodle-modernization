using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moodle.Infrastructure.Files.Migrations
{
    /// <inheritdoc />
    public partial class FileEntryUniqueActiveOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_file_entry_ContextId_Component_Area_ItemId_Path_Name",
                table: "file_entry");

            migrationBuilder.CreateIndex(
                name: "IX_file_entry_ContextId_Component_Area_ItemId_Path_Name",
                table: "file_entry",
                columns: new[] { "ContextId", "Component", "Area", "ItemId", "Path", "Name" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_file_entry_ContextId_Component_Area_ItemId_Path_Name",
                table: "file_entry");

            migrationBuilder.CreateIndex(
                name: "IX_file_entry_ContextId_Component_Area_ItemId_Path_Name",
                table: "file_entry",
                columns: new[] { "ContextId", "Component", "Area", "ItemId", "Path", "Name" },
                unique: true);
        }
    }
}
