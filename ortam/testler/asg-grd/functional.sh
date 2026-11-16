#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$(realpath "${BASH_SOURCE[0]}")")/common.sh"
prepare_output "$@"
find_targets
other=$(query "SELECT id FROM m_user WHERE username='ogrenci2'")
[[ $other =~ ^[0-9]+$ ]]
sid=$(query "SELECT id FROM m_assign_submission WHERE userid=$other AND assignment=(SELECT instance FROM m_course_modules WHERE id=$CM_ID) AND latest=1")
[[ $sid =~ ^[0-9]+$ ]]

# This phase temporarily redirects the local web app to a disposable fixture copy.
exec 9>"$LAB/.asg-grd-functional.lock"
flock -n 9
config="$MOODLE_DOCKER_WWWROOT/config.php"
backup=$(mktemp "$LAB/.asg-grd-config.XXXXXXXX")
cp "$config" "$backup"
db=wbs_final_$(date +%s)_$$
root=''
created=0
restore() {
    cp "$backup" "$config"
    rm -f -- "$backup"
    if ((created)); then "$compose" exec -T db dropdb --force -U moodle "$db"; fi
    if [[ "$root" == /tmp/wbs-final.* ]]; then
        "$compose" exec -T webserver rm -rf -- "$root"
    fi
}
trap restore EXIT
"$compose" exec -T db createdb -U moodle "$db"
created=1
"$compose" exec -T db pg_dump -U moodle -T 'public.b_*' moodle |
    "$compose" exec -T db psql -Xq -v ON_ERROR_STOP=1 -U moodle -d "$db" > "$OUTPUT/clone.log"
root=$("$compose" exec -T -u www-data webserver mktemp -d /tmp/wbs-final.XXXXXXXX)
python3 - "$config" "$db" "$root" <<'PY'
from pathlib import Path
import sys
config, database, dataroot = sys.argv[1:]
path = Path(config)
source = path.read_text()
anchor = "require_once(__DIR__ . '/lib/setup.php');"
if source.count(anchor) != 1:
    raise SystemExit('Unexpected Moodle Docker config template')
override = (
    f"$CFG->dbname = '{database}';\n"
    f"$CFG->dataroot = '{dataroot}';\n"
    "$CFG->debug = 0;\n$CFG->debugdisplay = 0;\n"
)
path.write_text(source.replace(anchor, override + anchor))
PY
python3 "$SCRIPTS/verify_pages.py" "$COURSE_ID" "$CM_ID" "$other" "$sid" | tee "$OUTPUT/production-permissions.log"
ASG_GRD_ISOLATED_DB="$db" python3 "$SCRIPTS/cutoff.py" "$CM_ID" "$db" | tee "$OUTPUT/cutoff.log"
"$compose" exec -T webserver php < "$SCRIPTS/grade-protection.php" | tee "$OUTPUT/grade-protection.log"
trap - EXIT
restore

feature=public/mod/assign/tests/behat/wbs_asg_grd_characterization.feature
target="$MOODLE_DOCKER_WWWROOT/$feature"
test ! -e "$target"
cp "$SCRIPTS/characterization.feature" "$target"
cleanup_behat() {
    rm -f -- "$target"
    "$compose" exec -T webserver php public/admin/tool/behat/cli/util.php --enable > "$OUTPUT/behat-cleanup.log" 2>&1 || true
}
trap cleanup_behat EXIT
"$compose" exec -T webserver php public/admin/tool/behat/cli/init.php --disable-composer > "$OUTPUT/behat-init.log" 2>&1
run_behat() {
    "$compose" exec -T webserver php public/admin/tool/behat/cli/run.php --feature="/var/www/html/$1" \
        --format=pretty --no-colors > "$OUTPUT/$2.log" 2>&1
    tail -n 4 "$OUTPUT/$2.log"
}
run_behat "$feature" characterization
"$compose" exec -T db psql -X -v ON_ERROR_STOP=1 -U moodle -d moodle <<'SQL' | tee "$OUTPUT/grade-attempt.log"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM b_assign_grades g JOIN b_assign a ON a.id=g.assignment
        JOIN b_user u ON u.id=g.userid JOIN b_assign_submission s
        ON (s.assignment,s.userid,s.attemptnumber)=(g.assignment,g.userid,g.attemptnumber)
        WHERE a.name='Numeric assignment' AND u.username='student1' AND g.attemptnumber=0 AND g.grade=100
    ) THEN RAISE EXCEPTION 'Expected matching attempt 0 with grade 100';
    END IF;
END $$;
SELECT g.attemptnumber AS grade_attempt, s.attemptnumber AS submission_attempt, g.grade
FROM b_assign_grades g JOIN b_assign a ON a.id=g.assignment JOIN b_user u ON u.id=g.userid
JOIN b_assign_submission s ON (s.assignment,s.userid,s.attemptnumber)=(g.assignment,g.userid,g.attemptnumber)
WHERE a.name='Numeric assignment' AND u.username='student1';
SQL
run_behat public/grade/report/user/tests/behat/hidden_grades.feature hidden_grades
run_behat public/grade/report/grader/tests/behat/locking.feature locking
run_behat public/grade/report/grader/tests/behat/grade_override.feature grade_override
