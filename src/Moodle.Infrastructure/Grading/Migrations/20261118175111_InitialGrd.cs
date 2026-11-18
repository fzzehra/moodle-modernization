using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Moodle.Infrastructure.Grading.Migrations
{
    /// <inheritdoc />
    public partial class InitialGrd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grd_assignment_grades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssignmentId = table.Column<int>(type: "integer", nullable: false),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    GraderId = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(10,5)", precision: 10, scale: 5, nullable: true),
                    Feedback = table.Column<string>(type: "text", nullable: true),
                    GradedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grd_assignment_grades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "grd_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Aggregation = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grd_categories", x => x.Id);
                    table.UniqueConstraint("AK_grd_categories_CourseId_Id", x => new { x.CourseId, x.Id });
                    table.ForeignKey(
                        name: "FK_grd_categories_grd_categories_CourseId_ParentId",
                        columns: x => new { x.CourseId, x.ParentId },
                        principalTable: "grd_categories",
                        principalColumns: new[] { "CourseId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grd_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    AssignmentId = table.Column<int>(type: "integer", nullable: true),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MinScore = table.Column<decimal>(type: "numeric(10,5)", precision: 10, scale: 5, nullable: false),
                    MaxScore = table.Column<decimal>(type: "numeric(10,5)", precision: 10, scale: 5, nullable: false),
                    Hidden = table.Column<bool>(type: "boolean", nullable: false),
                    HiddenUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Locked = table.Column<bool>(type: "boolean", nullable: false),
                    LockAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grd_items", x => x.Id);
                    table.CheckConstraint("CK_grd_items_score_range", "\"MinScore\" <= \"MaxScore\"");
                    table.ForeignKey(
                        name: "FK_grd_items_grd_categories_CourseId_CategoryId",
                        columns: x => new { x.CourseId, x.CategoryId },
                        principalTable: "grd_categories",
                        principalColumns: new[] { "CourseId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grd_grades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    RawScore = table.Column<decimal>(type: "numeric(10,5)", precision: 10, scale: 5, nullable: true),
                    FinalScore = table.Column<decimal>(type: "numeric(10,5)", precision: 10, scale: 5, nullable: true),
                    Hidden = table.Column<bool>(type: "boolean", nullable: false),
                    HiddenUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Locked = table.Column<bool>(type: "boolean", nullable: false),
                    LockAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Overridden = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grd_grades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_grd_grades_grd_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "grd_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_grd_assignment_grades_AssignmentId_LearnerId_Attempt",
                table: "grd_assignment_grades",
                columns: new[] { "AssignmentId", "LearnerId", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grd_categories_CourseId_ParentId",
                table: "grd_categories",
                columns: new[] { "CourseId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_grd_grades_ItemId_LearnerId",
                table: "grd_grades",
                columns: new[] { "ItemId", "LearnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grd_items_AssignmentId",
                table: "grd_items",
                column: "AssignmentId",
                unique: true,
                filter: "\"AssignmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_grd_items_CourseId_CategoryId",
                table: "grd_items",
                columns: new[] { "CourseId", "CategoryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grd_assignment_grades");

            migrationBuilder.DropTable(
                name: "grd_grades");

            migrationBuilder.DropTable(
                name: "grd_items");

            migrationBuilder.DropTable(
                name: "grd_categories");
        }
    }
}
