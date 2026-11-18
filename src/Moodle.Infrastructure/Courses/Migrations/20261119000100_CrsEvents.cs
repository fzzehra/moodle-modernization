using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moodle.Infrastructure.Courses.Migrations;

public partial class CrsEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE crs_events (
            "Id" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            "Kind" varchar(40) NOT NULL,
            "CourseId" integer NOT NULL,
            "EnrolmentId" integer NULL,
            "UserId" integer NULL,
            "OccurredAt" timestamp with time zone NOT NULL DEFAULT clock_timestamp(),
            "Version" integer NOT NULL DEFAULT 1,
            "ChangedFields" text[] NOT NULL
        );
        -- Deliberately no FK to live entities: deletion history must survive cascades.
        CREATE TABLE crs_event_receipts (
            "EventId" bigint NOT NULL REFERENCES crs_events("Id") ON DELETE RESTRICT,
            "ExtensionId" varchar(100) NOT NULL,
            PRIMARY KEY ("EventId", "ExtensionId")
        );
        CREATE FUNCTION crs_record_change() RETURNS trigger LANGUAGE plpgsql AS $body$
        DECLARE
            current_row jsonb;
            previous_row jsonb;
            fields text[];
            entity_kind text;
        BEGIN
            IF TG_OP = 'UPDATE' AND NEW IS NOT DISTINCT FROM OLD THEN
                RETURN NEW;
            END IF;
            IF TG_OP = 'DELETE' THEN current_row := to_jsonb(OLD);
            ELSE current_row := to_jsonb(NEW); END IF;
            IF TG_OP = 'UPDATE' THEN previous_row := to_jsonb(OLD);
            ELSE previous_row := '{}'::jsonb; END IF;
            SELECT COALESCE(array_agg(key ORDER BY key), ARRAY[]::text[])
                INTO fields FROM jsonb_each(current_row)
                WHERE TG_OP <> 'UPDATE' OR value IS DISTINCT FROM previous_row -> key;
            IF TG_TABLE_NAME = 'crs_courses' THEN entity_kind := 'course';
            ELSE entity_kind := 'enrolment'; END IF;
            INSERT INTO crs_events ("Kind", "CourseId", "EnrolmentId", "UserId", "ChangedFields")
            VALUES (
                entity_kind || CASE TG_OP WHEN 'INSERT' THEN '.created'
                    WHEN 'UPDATE' THEN '.updated' ELSE '.deleted' END,
                CASE WHEN entity_kind = 'course' THEN (current_row ->> 'Id')::integer
                    ELSE (current_row ->> 'CourseId')::integer END,
                CASE WHEN entity_kind = 'enrolment' THEN (current_row ->> 'Id')::integer END,
                CASE WHEN entity_kind = 'enrolment' THEN (current_row ->> 'UserId')::integer END,
                fields);
            IF TG_OP = 'DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
        END;
        $body$;
        CREATE TRIGGER crs_course_changed AFTER INSERT OR UPDATE OR DELETE ON crs_courses
            FOR EACH ROW EXECUTE FUNCTION crs_record_change();
        CREATE TRIGGER crs_enrolment_changed AFTER INSERT OR UPDATE OR DELETE ON crs_enrolments
            FOR EACH ROW EXECUTE FUNCTION crs_record_change();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TRIGGER crs_enrolment_changed ON crs_enrolments;
        DROP TRIGGER crs_course_changed ON crs_courses;
        DROP FUNCTION crs_record_change();
        DROP TABLE crs_event_receipts;
        DROP TABLE crs_events;
        """);
}
