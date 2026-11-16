import datetime as dt
import json
from collections import defaultdict
from pathlib import Path
from statistics import mean, median
import sys

def mib(text):
    for unit, factor in [('GiB', 1024), ('MiB', 1), ('KiB', 1 / 1024), ('B', 1 / 1024**2)]:
        if text.endswith(unit):
            return float(text.removesuffix(unit)) * factor
    raise ValueError(text)

def timestamp(value):
    return dt.datetime.fromisoformat(value.replace('Z', '+00:00'))

root = Path(sys.argv[1])
results = {}
samples = defaultdict(list)
for target in ('list', 'course', 'assignment', 'grades'):
    for vus in (5, 50):
        runs = []
        for repetition in range(1, 6):
            prefix = root / f'{target}-{vus}-{repetition}'
            summary = json.loads(prefix.with_suffix('.summary.json').read_text())
            metrics = summary['metrics']
            trend = metrics['page_response_ms']['values']
            count = metrics['page_requests']['values']['count']
            error = metrics['page_failure_rate']['values']['rate']
            runs.append(dict(mean_ms=trend['avg'], median_ms=trend['med'], p95_ms=trend['p(95)'],
                             requests=count, rps=count / 15, error_pct=100 * error))
            with prefix.with_suffix('.points.json').open() as stream:
                times = [timestamp(x['data']['time']) for line in stream
                         if (x := json.loads(line))['type'] == 'Point' and x['metric'] == 'page_requests']
            for line in prefix.with_suffix('.stats.txt').read_text().splitlines():
                stamp, name, cpu, ram = line.split('|')
                if min(times) <= timestamp(stamp) <= max(times):
                    key = (target, vus, name)
                    samples[key].append((float(cpu.rstrip('%')), mib(ram.split(' / ')[0])))
        results[target, vus] = runs

print('| Sayfa | VU | Ort. ms | Medyan ms | p95 ms | İstek/sn | Hata % |')
print('| --- | ---: | ---: | ---: | ---: | ---: | ---: |')
for (target, vus), runs in results.items():
    values = [mean(r['mean_ms'] for r in runs), median(r['median_ms'] for r in runs),
              median(r['p95_ms'] for r in runs), mean(r['rps'] for r in runs),
              sum(r['error_pct'] * r['requests'] for r in runs) / sum(r['requests'] for r in runs)]
    print(f'| {target} | {vus} | ' + ' | '.join(f'{x:.2f}' for x in values) + ' |')
print()
print('| Sayfa | VU | Container | CPU ort. % | CPU tepe % | RAM ort. MiB | RAM tepe MiB | Örnek |')
print('| --- | ---: | --- | ---: | ---: | ---: | ---: | ---: |')
for (target, vus, name), data in sorted(samples.items()):
    cpu, ram = zip(*data)
    print(f'| {target} | {vus} | {name} | {mean(cpu):.2f} | {max(cpu):.2f} | {mean(ram):.2f} | {max(ram):.2f} | {len(data)} |')
(root / 'per-run.json').write_text(json.dumps({f'{target}-{vus}': runs for (target, vus), runs in results.items()}, indent=2) + '\n')
