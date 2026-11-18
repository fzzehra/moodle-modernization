using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Moodle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AsgInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: false),
                    OpensAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CutoffAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequiresConfirmation = table.Column<bool>(type: "boolean", nullable: false),
                    EnabledSubmissionTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assignments", x => x.Id);
                    table.CheckConstraint("CK_Assignment_Dates", "(\"OpensAt\" IS NULL OR \"DueAt\" IS NULL OR \"OpensAt\" <= \"DueAt\") AND (\"DueAt\" IS NULL OR \"CutoffAt\" IS NULL OR \"DueAt\" <= \"CutoffAt\") AND (\"OpensAt\" IS NULL OR \"CutoffAt\" IS NULL OR \"OpensAt\" <= \"CutoffAt\")");
                    table.CheckConstraint("CK_Assignment_References", "(\"CourseId\" > 0 AND \"SectionId\" > 0 AND \"Version\" > 0)");
                    table.CheckConstraint("CK_Assignment_Title", "length(btrim(\"Title\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "SubmissionRules",
                columns: table => new
                {
                    AssignmentId = table.Column<int>(type: "integer", nullable: false),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    ExtendedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Locked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionRules", x => new { x.AssignmentId, x.LearnerId });
                    table.CheckConstraint("CK_Rule_Learner", "(\"LearnerId\" > 0)");
                    table.ForeignKey(
                        name: "FK_SubmissionRules_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Submissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssignmentId = table.Column<int>(type: "integer", nullable: false),
                    LearnerId = table.Column<int>(type: "integer", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.Id);
                    table.CheckConstraint("CK_Submission_State", "(\"Status\" = 'draft' AND \"SubmittedAt\" IS NULL) OR (\"Status\" = 'submitted' AND \"SubmittedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_Submission_Values", "(\"Attempt\" = 0 AND \"LearnerId\" > 0 AND \"Version\" > 0 AND length(btrim(\"Text\")) > 0)");
                    table.ForeignKey(
                        name: "FK_Submissions_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_CourseId_SectionId",
                table: "Assignments",
                columns: new[] { "CourseId", "SectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_AssignmentId_LearnerId_Attempt",
                table: "Submissions",
                columns: new[] { "AssignmentId", "LearnerId", "Attempt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubmissionRules");

            migrationBuilder.DropTable(
                name: "Submissions");

            migrationBuilder.DropTable(
                name: "Assignments");
        }
    }
}
