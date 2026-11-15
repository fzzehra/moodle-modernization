from pathlib import Path
from subprocess import check_output
from html.parser import HTMLParser

from verify_pages import fetch, login
import os
import sys

assignment, database = sys.argv[1:]
assert database.startswith('wbs_final_') and os.environ.get('ASG_GRD_ISOLATED_DB') == database

class Form(HTMLParser):
    def __init__(self):
        super().__init__()
        self.inputs = {}
    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == 'input' and attrs.get('type') == 'hidden' and 'name' in attrs:
            self.inputs[attrs['name']] = attrs.get('value', '')
def sql(statement):
    return check_output(['bin/moodle-docker-compose','exec','-T','db','psql','-XAtq',
                         '-v','ON_ERROR_STOP=1','-U','moodle','-d',database,'-c',statement],text=True).strip()
client = login('ogrenci7')
path = '/mod/assign/view.php?id=' + assignment
code, url, body = fetch(client, path + '&action=editsubmission')
form = Form()
form.feed(body)
assert code == 200 and form.inputs.get('action') == 'savesubmission' and form.inputs.get('sesskey')
fields = form.inputs
fields['onlinetext_editor[text]'] = 'Synthetic deadline validation'
fields.setdefault('onlinetext_editor[format]', '1')
fields['submitbutton'] = 'Save changes'
where = """FROM m_assign_submission s JOIN m_user u ON u.id=s.userid JOIN m_course_modules cm ON cm.instance=s.assignment JOIN m_modules m ON m.id=cm.module AND m.name='assign' LEFT JOIN m_assignsubmission_onlinetext t ON t.submission=s.id WHERE u.username='ogrenci7' AND cm.id=""" + str(int(assignment))
snapshot = 'SELECT s.id,s.status,s.timemodified,coalesce(t.onlinetext, \'\') ' + where
before = sql(snapshot)
sql("UPDATE m_assign SET allowsubmissionsfromdate=0, duedate=extract(epoch from now())::bigint-120, cutoffdate=extract(epoch from now())::bigint-60 WHERE id=(SELECT instance FROM m_course_modules WHERE id="+str(int(assignment))+")")
code, url, body = fetch(client, path, fields)
after = sql(snapshot)
denied = any(s in body for s in ('The due date for this assignment has now passed','This assignment is not open for submissions','This assignment is closed','Submissions closed'))
print(f'T-ASG-04 HTTP POST: HTTP {code}; cutoff message={denied}; stored state unchanged={before == after}')
assert '/login/' not in url and (denied or code == 404) and before == after
sql("UPDATE m_assign SET duedate=0, cutoffdate=0 WHERE id=(SELECT instance FROM m_course_modules WHERE id="+str(int(assignment))+")")
code, url, _ = fetch(client, path, fields)
accepted = 'Synthetic deadline validation' in sql(snapshot)
print(f'T-ASG-04 positive control after reopening: HTTP {code}; text saved={accepted}')
assert code == 200 and '/login/' not in url and accepted
