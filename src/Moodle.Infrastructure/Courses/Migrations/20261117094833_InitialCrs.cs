using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Moodle.Infrastructure.Courses.Migrations
{
    /// <inheritdoc />
    public partial class InitialCrs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "crs_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ParentId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crs_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crs_categories_crs_categories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "crs_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "crs_courses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    FullName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IdNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Visible = table.Column<bool>(type: "boolean", nullable: false),
                    StartDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crs_courses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crs_courses_crs_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "crs_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "crs_enrolments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Suspended = table.Column<bool>(type: "boolean", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crs_enrolments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crs_enrolments_crs_courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "crs_courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_crs_categories_ParentId",
                table: "crs_categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_crs_courses_CategoryId",
                table: "crs_courses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_crs_courses_IdNumber",
                table: "crs_courses",
                column: "IdNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_crs_courses_ShortName",
                table: "crs_courses",
                column: "ShortName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_crs_enrolments_CourseId_UserId",
                table: "crs_enrolments",
                columns: new[] { "CourseId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crs_enrolments");

            migrationBuilder.DropTable(
                name: "crs_courses");

            migrationBuilder.DropTable(
                name: "crs_categories");
        }
    }
}
