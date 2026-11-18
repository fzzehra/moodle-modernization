using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Moodle.Infrastructure.Files.Migrations
{
    /// <inheritdoc />
    public partial class InitialFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "file_blob",
                columns: table => new
                {
                    ContentHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OrphanedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_blob", x => x.ContentHash);
                });

            migrationBuilder.CreateTable(
                name: "file_draft",
                columns: table => new
                {
                    DraftId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_draft", x => x.DraftId);
                });

            migrationBuilder.CreateTable(
                name: "file_setting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MaxFileBytes = table.Column<long>(type: "bigint", nullable: false),
                    UserQuotaBytes = table.Column<long>(type: "bigint", nullable: false),
                    DraftTtlHours = table.Column<int>(type: "integer", nullable: false),
                    TrashRetentionHours = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_setting", x => x.Id);
                    table.CheckConstraint("ck_file_setting_single_row", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "file_entry",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContentHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ContextId = table.Column<int>(type: "integer", nullable: false),
                    Component = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Area = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    Path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OwnerUserId = table.Column<int>(type: "integer", nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    MimeType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Author = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    License = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ReferenceId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_entry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_file_entry_file_blob_ContentHash",
                        column: x => x.ContentHash,
                        principalTable: "file_blob",
                        principalColumn: "ContentHash",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "file_setting",
                columns: new[] { "Id", "DraftTtlHours", "MaxFileBytes", "TrashRetentionHours", "UserQuotaBytes" },
                values: new object[] { 1, 96, 52428800L, 24, 104857600L });

            migrationBuilder.CreateIndex(
                name: "IX_file_blob_OrphanedAt",
                table: "file_blob",
                column: "OrphanedAt");

            migrationBuilder.CreateIndex(
                name: "IX_file_draft_LastActivityAt",
                table: "file_draft",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_file_entry_Component_Area_ContextId_ItemId",
                table: "file_entry",
                columns: new[] { "Component", "Area", "ContextId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_file_entry_ContentHash",
                table: "file_entry",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_file_entry_ContextId_Component_Area_ItemId_Path_Name",
                table: "file_entry",
                columns: new[] { "ContextId", "Component", "Area", "ItemId", "Path", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_entry_OwnerUserId_Component",
                table: "file_entry",
                columns: new[] { "OwnerUserId", "Component" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_draft");

            migrationBuilder.DropTable(
                name: "file_entry");

            migrationBuilder.DropTable(
                name: "file_setting");

            migrationBuilder.DropTable(
                name: "file_blob");
        }
    }
}
