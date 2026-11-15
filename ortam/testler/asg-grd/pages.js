import http from 'k6/http';
import { sleep } from 'k6';
import exec from 'k6/execution';
import { Counter, Rate, Trend } from 'k6/metrics';

const base = 'http://localhost:8000';
const login = base + '/login/index.php';
const vus = Number(__ENV.VUS);
const seconds = Number(__ENV.SECONDS_PER_RUN);
const targets = {
    list: ['/course/index.php', 'page-course-index'],
    course: ['/course/view.php?id=' + __ENV.COURSE_ID, 'page-course-view-'],
    assignment: ['/mod/assign/view.php?id=' + __ENV.CM_ID, 'page-mod-assign-view'],
    grades: ['/grade/report/user/index.php?id=' + __ENV.COURSE_ID, 'page-grade-report-user-index'],
};
const [path, page] = targets[__ENV.TARGET];
const hits = new Counter('page_requests');
const latency = new Trend('page_response_ms', true);
const failures = new Rate('page_failure_rate');
export const options = {
    noCookiesReset: true,
    setupTimeout: '5m',
    summaryTrendStats: ['avg', 'med', 'p(95)'],
    scenarios: {
        read: { executor: 'constant-vus', vus, duration: seconds + 's', gracefulStop: '15s' },
    },
};

export function setup() {
    const jar = http.cookieJar();
    const sessions = [];
    for (let i = 0; i < vus; i++) {
        jar.clear(base);
        const account = 'ogrenci' + ((i % 10) + 1);
        const form = http.get(login);
        const token = form.body.match(/name="logintoken"[^>]*value="([^"]+)"/);
        if (!token) throw new Error('Login form token missing');
        const signedIn = http.post(login, {
            username: account, password: JSON.parse(__ENV.ASG_GRD_CREDENTIALS)[account], logintoken: token[1],
        });
        if (signedIn.status !== 200 || signedIn.url.includes('/login/')) {
            throw new Error('Sample user login unsuccessful');
        }
        const cookie = jar.cookiesForURL(base).MoodleSession;
        if (!cookie || !cookie[0]) throw new Error('Moodle session missing');
        sessions.push(cookie[0]);
    }
    if (new Set(sessions).size !== vus) throw new Error('Duplicate session identifier');
    return sessions;
}

export default function (sessions) {
    const jar = http.cookieJar();
    if (exec.vu.iterationInScenario === 0) {
        jar.set(base, 'MoodleSession', sessions[exec.vu.idInTest - 1], { path: '/' });
    }
    const response = http.get(base + path, { redirects: 0, timeout: '10s' });
    const expected = response.status === 200
        && response.url === base + path
        && response.body.includes('id="' + page);
    hits.add(1);
    latency.add(response.timings.duration);
    failures.add(!expected);
    sleep(1);
}

export function handleSummary(data) {
    return { [__ENV.SUMMARY]: JSON.stringify(data, null, 2) + '\n' };
}
