// FILE-NFR-01 / T-FILE-15: 50 eş zamanlı kullanıcı aynı dosyayı indirir, p95 ölçülür.
// Eski sistem ölçümüyle (WBS-1 §4.6.10, T-FILE-15) karşılaştırılabilir olması için aynı yöntem:
// tüm sanal kullanıcılar TEK bir oturumu paylaşır (ayrı ayrı 50 oturum açılmaz).
import http from 'k6/http';
import { sleep } from 'k6';
import exec from 'k6/execution';
import { Counter, Rate, Trend } from 'k6/metrics';

const base = __ENV.BASE || 'http://host.docker.internal:8080';
const fileId = __ENV.FILE_ID;
const vus = Number(__ENV.VUS);
const seconds = Number(__ENV.SECONDS_PER_RUN);

const hits = new Counter('file_requests');
const latency = new Trend('file_response_ms', true);
const failures = new Rate('file_failure_rate');

export const options = {
    noCookiesReset: true,
    setupTimeout: '1m',
    summaryTrendStats: ['avg', 'med', 'p(95)'],
    scenarios: {
        download: { executor: 'constant-vus', vus, duration: seconds + 's', gracefulStop: '15s' },
    },
};

export function setup() {
    const jar = http.cookieJar();
    const signedIn = http.post(base + '/api/v1/auth/login', JSON.stringify({ username: 'ogretmen1', password: 'ogretmen1' }),
        { headers: { 'Content-Type': 'application/json' } });
    if (signedIn.status !== 200) throw new Error('Login failed: ' + signedIn.status + ' ' + signedIn.body);
    const cookie = jar.cookiesForURL(base)['.AspNetCore.Identity.Application'];
    if (!cookie || !cookie[0]) throw new Error('Identity session cookie missing');
    return cookie[0];
}

export default function (sessionCookie) {
    const jar = http.cookieJar();
    if (exec.vu.iterationInScenario === 0) {
        jar.set(base, '.AspNetCore.Identity.Application', sessionCookie, { path: '/' });
    }
    const response = http.get(base + '/api/v1/files/' + fileId + '/content', { redirects: 0, timeout: '30s' });
    const expected = response.status === 200;
    hits.add(1);
    latency.add(response.timings.duration);
    failures.add(!expected);
    sleep(1);
}

export function handleSummary(data) {
    return { stdout: JSON.stringify(data, null, 2) + '\n' };
}
