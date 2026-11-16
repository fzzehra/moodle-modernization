<?php
define('CLI_SCRIPT', true);
define('CACHE_DISABLE_ALL', true);
require '/var/www/html/config.php';
require_once($CFG->dirroot.'/mod/assign/lib.php');
require_once($CFG->libdir.'/gradelib.php');
if (!str_starts_with($CFG->dbname, 'wbs_final_')) {
    throw new RuntimeException('Expected the isolated database');
}
$assign = $DB->get_record('assign', ['name' => 'SENG101 Ödev 1'], '*', MUST_EXIST);
$assign->cmidnumber = 'seng101a1';
$item = $DB->get_record('grade_items', [
    'courseid' => $assign->course, 'itemmodule' => 'assign',
    'iteminstance' => $assign->id, 'itemnumber' => 0,
], '*', MUST_EXIST);
foreach (['locked' => 'ogrenci1', 'overridden' => 'ogrenci2', 'unprotected' => 'ogrenci3'] as $mode => $username) {
    $user = $DB->get_record('user', ['username' => $username], '*', MUST_EXIST);
    $grade = $DB->get_record('grade_grades', ['itemid' => $item->id, 'userid' => $user->id], '*', MUST_EXIST);
    if ($mode !== 'unprotected') {
        $DB->set_field('grade_grades', $mode, time(), ['id' => $grade->id]);
    }
    $status = assign_grade_item_update($assign, (object)['userid' => $user->id, 'rawgrade' => 88]);
    grade_regrade_final_grades($assign->course);
    $after = $DB->get_field('grade_grades', 'finalgrade', ['id' => $grade->id], MUST_EXIST);
    printf("%s: before=%s after=%s grade_update=%s\n", $mode, $grade->finalgrade, $after, $status);
    $expected = $mode === 'unprotected' ? 88.0 : (float)$grade->finalgrade;
    if ((float)$after !== $expected) {
        throw new RuntimeException('Unexpected outcome for '.$mode);
    }
}
echo "PASS: locked and overridden grades preserved; unprotected grade updated\n";
