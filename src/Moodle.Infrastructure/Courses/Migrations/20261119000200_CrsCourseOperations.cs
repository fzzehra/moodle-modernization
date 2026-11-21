using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moodle.Infrastructure.Courses.Migrations;

public partial class CrsCourseOperations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE crs_course_operations (
            "Id" uuid PRIMARY KEY,
            "CourseId" integer NOT NULL REFERENCES crs_courses("Id") ON DELETE RESTRICT,
            "Kind" integer NOT NULL CHECK ("Kind" IN (1, 2)),
            "State" integer NOT NULL CHECK ("State" IN (1, 2)),
            "CreatedAt" timestamp with time zone NOT NULL DEFAULT clock_timestamp()
        );
        CREATE INDEX "IX_crs_course_operations_CourseId" ON crs_course_operations("CourseId");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        -- Never silently remove protection from pending/running work during downgrade.
        LOCK TABLE crs_course_operations IN ACCESS EXCLUSIVE MODE;
        DO $body$ BEGIN
            IF EXISTS (SELECT 1 FROM crs_course_operations) THEN
                RAISE EXCEPTION 'Cannot drop CRS operation protection while operations exist';
            END IF;
        END; $body$;
        DROP TABLE crs_course_operations;
        """);
}
