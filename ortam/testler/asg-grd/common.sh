# shellcheck shell=bash
SCRIPTS=$(dirname "$(realpath "${BASH_SOURCE[0]}")")
LAB=$(realpath "${MOODLE_LAB:-$HOME/moodle-lab}")
export MOODLE_DOCKER_WWWROOT="$LAB/moodle" MOODLE_DOCKER_DB=pgsql MOODLE_DOCKER_PHP_VERSION=8.3
export PYTHONDONTWRITEBYTECODE=1
: "${ASG_GRD_CREDENTIALS:?Load the local test credential JSON as documented in README.md}"
cd "$LAB/moodle-docker"
compose=bin/moodle-docker-compose

prepare_output() {
    umask 077
    OUTPUT=$(realpath -m "${1:-$LAB/asg-grd-runs}")
    local repository
    repository=$(git -C "$SCRIPTS" rev-parse --show-toplevel)
    repository=$(realpath "$repository")
    case "$OUTPUT/" in
        "$repository/"*) echo 'Output must be outside the repository' >&2; return 1 ;;
    esac
    mkdir -p "$OUTPUT"
    OUTPUT=$(mktemp -d "$OUTPUT/run.XXXXXXXX")
    printf 'Output: %s\n' "$OUTPUT"
}

query() {
    "$compose" exec -T db psql -X -v ON_ERROR_STOP=1 -Aqt -U moodle -d "${TEST_DB:-moodle}" -c "$1"
}

find_targets() {
    COURSE_ID=$(query "SELECT id FROM m_course WHERE shortname='SENG101'")
    [[ $COURSE_ID =~ ^[0-9]+$ ]]
    CM_ID=$(query "SELECT id FROM m_course_modules WHERE course=$COURSE_ID AND idnumber='seng101a1'")
    [[ $CM_ID =~ ^[0-9]+$ ]]
}
