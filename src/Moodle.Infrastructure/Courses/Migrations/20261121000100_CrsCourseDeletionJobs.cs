using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moodle.Infrastructure.Courses.Migrations;

// YZ destekli (Claude Code): CRS-FR-11 / CRS-NFR-06 kalıcı ders silme kuyruğu.
public partial class CrsCourseDeletionJobs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        -- Deliberately no FK to crs_courses: job history must survive the deletion it performs.
        CREATE TABLE crs_course_deletion_jobs (
            "Id" uuid PRIMARY KEY,
            "CourseId" integer NOT NULL,
            "CategoryId" integer NULL,
            "State" integer NOT NULL CHECK ("State" IN (1, 2, 3, 4)),
            "Attempts" integer NOT NULL DEFAULT 0 CHECK ("Attempts" >= 0),
            "RequestedByUserId" integer NULL,
            "CreatedAt" timestamp with time zone NOT NULL DEFAULT clock_timestamp(),
            "StartedAt" timestamp with time zone NULL,
            "CompletedAt" timestamp with time zone NULL,
            "ErrorCode" varchar(64) NULL,
            "ErrorMessage" varchar(500) NULL
        );
        -- At most one pending (1) or running (2) deletion per course.
        CREATE UNIQUE INDEX "IX_crs_course_deletion_jobs_CourseId_Active"
            ON crs_course_deletion_jobs ("CourseId") WHERE "State" IN (1, 2);
        CREATE INDEX "IX_crs_course_deletion_jobs_Pending"
            ON crs_course_deletion_jobs ("CreatedAt", "Id") WHERE "State" = 1;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        -- Never silently discard accepted (pending/running) deletion requests during downgrade.
        LOCK TABLE crs_course_deletion_jobs IN ACCESS EXCLUSIVE MODE;
        DO $body$ BEGIN
            IF EXISTS (SELECT 1 FROM crs_course_deletion_jobs WHERE "State" IN (1, 2)) THEN
                RAISE EXCEPTION 'Cannot drop CRS deletion jobs while pending or running jobs exist';
            END IF;
        END; $body$;
        DROP TABLE crs_course_deletion_jobs;
        """);
}
