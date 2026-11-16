import html
from http.cookiejar import CookieJar
import re
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import HTTPCookieProcessor, Request, build_opener
import sys
import os
import json

base = 'http://localhost:8000'

def fetch(client, path, fields=None):
    request = Request(base + path, data=urlencode(fields).encode() if fields else None)
    try:
        response = client.open(request, timeout=20)
    except HTTPError as error:
        response = error
    with response:
        return response.code, response.url, html.unescape(response.read().decode('utf-8', errors='replace'))

def login(name):
    client = build_opener(HTTPCookieProcessor(CookieJar()))
    _, _, form = fetch(client, '/login/index.php')
    token = re.search(r'name="logintoken"[^>]*value="([^"]+)"', form)
    assert token, 'Login token absent'
    code, url, _ = fetch(client, '/login/index.php', {
        'username': name, 'password': json.loads(os.environ['ASG_GRD_CREDENTIALS'])[name], 'logintoken': token.group(1),
    })
    assert code == 200 and '/login/' not in url, 'Login failed'
    return client

def main():
    course, assignment, other, submission = sys.argv[1:5]
    student = login('ogrenci1')
    teacher = login('ogretmen1')
    assign_path = f'/mod/assign/view.php?id={assignment}'
    grade_path = f'/grade/report/user/index.php?id={course}'
    results = []

    def expect(name, condition, detail):
        results.append(bool(condition))
        print(f'{name}: {"PASS" if condition else "FAIL"} ({detail})')

    code, url, body = fetch(student, grade_path)
    rows = re.findall(r'<tr\b[^>]*>.*?</tr>', body, re.S)
    expect('T-GRD-02', code == 200 and '/login/' not in url
           and any('SENG101 Ödev 1' in row and re.search(r'>\s*85(?:[.,]00)?\s*<', row) for row in rows),
           f'HTTP {code}; own assignment grade 85')
    code, url, body = fetch(teacher, f'/grade/report/grader/index.php?id={course}')
    expect('T-GRD-03', code == 200 and 'page-grade-report-grader-index' in body
           and 'SENG101 Ödev 1' in body and 'Öğrenci 1' in body,
           f'HTTP {code}; grade item and enrolled learner')
    code, url, body = fetch(student, f'{grade_path}&userid={other}')
    expect('T-GRD-05', code == 404 and 'stack trace:' not in body.lower() and '/login/' not in url and 'Cannot view grades.' in body
           and not re.search(r'<table[^>]+user-grade', body),
           f'HTTP {code}; Moodle permission error and no grade table')
    code, url, body = fetch(teacher, f'{assign_path}&action=grading')
    expect('T-ASG-03', code == 200 and '/login/' not in url and 'Öğrenci 1' in body
           and 'Öğrenci 10' in body and 'Submitted for grading' in body and 'No submission' in body,
           f'HTTP {code}; submitted and missing submissions in teacher list')
    path = f'{assign_path}&action=viewpluginassignsubmission&plugin=onlinetext&sid={submission}'
    teacher_code, _, teacher_body = fetch(teacher, path)
    code, url, body = fetch(student, path)
    expect('T-ASG-05', code == 404 and 'stack trace:' not in body.lower() and teacher_code == 200 and 'Öğrenci 2 teslim metni' in teacher_body
           and '/login/' not in url and 'Öğrenci 2 teslim metni' not in body
           and ('nopermission' in body.lower() or 'do not currently have permissions' in body.lower()),
           f'HTTP {teacher_code}/{code}; teacher sees submission and student receives permission error')
    sys.exit(0 if all(results) else 1)


if __name__ == '__main__':
    main()
