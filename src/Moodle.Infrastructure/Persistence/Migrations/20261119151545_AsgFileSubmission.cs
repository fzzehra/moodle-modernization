using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moodle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AsgFileSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Submission_Values",
                table: "Submissions");

            migrationBuilder.AddColumn<int>(
                name: "FileCount",
                table: "Submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Submission_Values",
                table: "Submissions",
                sql: "(\"Attempt\" = 0 AND \"LearnerId\" > 0 AND \"Version\" > 0 AND \"FileCount\" >= 0 AND (length(btrim(\"Text\")) > 0 OR \"FileCount\" > 0))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Submission_Values",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "FileCount",
                table: "Submissions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Submission_Values",
                table: "Submissions",
                sql: "(\"Attempt\" = 0 AND \"LearnerId\" > 0 AND \"Version\" > 0 AND length(btrim(\"Text\")) > 0)");
        }
    }
}
