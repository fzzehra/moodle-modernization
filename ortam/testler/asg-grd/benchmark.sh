#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$(realpath "${BASH_SOURCE[0]}")")/common.sh"
command -v k6 >/dev/null
exec 9>"$LAB/.asg-grd-functional.lock"
flock -n 9
prepare_output "$@"
evidence=$OUTPUT
mkdir "$evidence/k6"
find_targets
export COURSE_ID CM_ID
mapfile -t containers < <("$compose" ps -q webserver db)
if (( ${#containers[@]} != 2 )); then
    echo "Expected running webserver and db containers" >&2
    exit 1
fi
{
    date --iso-8601=seconds
    uname -sr
    lscpu | sed -n '1,20p'
    free -m
    docker info --format 'Docker CPUs={{.NCPU}} RAM bytes={{.MemTotal}}'
    "$compose" exec -T webserver php -v
    query 'SELECT version()'
    git -C ../moodle rev-parse HEAD
    git rev-parse HEAD
    k6 version
    sha256sum ../moodle/ornek-veri.feature
    printf 'SENG101 course=%s assignment module=%s\n' "$COURSE_ID" "$CM_ID"
    query "SELECT 'users', count(*) FROM m_user WHERE username ~ '^(ogrenci[0-9]+|ogretmen[0-9]+|asistan1|danisman1|yonetici1)$' UNION ALL SELECT 'courses', count(*) FROM m_course WHERE shortname IN ('SENG101','SENG201','CENG301') UNION ALL SELECT 'assignments', count(*) FROM m_assign UNION ALL SELECT 'submissions', count(*) FROM m_assign_submission UNION ALL SELECT 'graded assignment items', count(*) FROM m_grade_grades g JOIN m_grade_items i ON i.id=g.itemid WHERE i.itemmodule='assign' AND g.finalgrade IS NOT NULL"
} > "$evidence/k6/environment.txt"

sampler=''
stop_sampler() {
    if [[ -n "$sampler" ]]; then
        kill "$sampler" 2>/dev/null || true
        wait "$sampler" 2>/dev/null || true
        sampler=''
    fi
}
trap stop_sampler EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

run() {
    local label=$1 rc=0
    export SECONDS_PER_RUN=$2
    local prefix="$evidence/k6/$TARGET-$VUS-$label"
    export SUMMARY="$prefix.summary.json"
    (
        while true; do
            stamp=$(date --iso-8601=ns)
            docker stats --no-stream --format '{{.Name}}|{{.CPUPerc}}|{{.MemUsage}}' "${containers[@]}" |
                sed "s/^/$stamp|/"
            sleep 1
        done
    ) > "$prefix.stats.txt" &
    sampler=$!
    k6 run --quiet --no-usage-report --system-tags=status,method,scenario,expected_response --out "json=$prefix.points.json" "$SCRIPTS/pages.js" > "$prefix.log" 2>&1 || rc=$?
    stop_sampler
    if ((rc != 0)); then
        cat "$prefix.log" >&2
        return "$rc"
    fi
    python3 - "$SUMMARY" <<'PY'
import json,sys
metrics=json.load(open(sys.argv[1]))['metrics']
if metrics['page_failure_rate']['values']['rate'] >= 1:
    raise SystemExit('No successful page response in ' + sys.argv[1])
PY
    printf '%s %s\n' "$(date --iso-8601=seconds)" "$prefix"
}
export TARGET=list VUS=1
run smoke 3
for TARGET in list course assignment grades; do
    for VUS in 5 50; do
        run warmup 10
        sleep 5
        for repetition in 1 2 3 4 5; do
            run "$repetition" 15
            sleep 5
        done
    done
done

python3 "$SCRIPTS/summarize.py" "$evidence/k6" > "$evidence/k6/results.md"
