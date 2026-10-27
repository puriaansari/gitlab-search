"""HTTP regression tests; run against the committed Postman simulation."""
import json, os, pathlib, shutil, signal, subprocess, tempfile, time, urllib.request

root = pathlib.Path(__file__).resolve().parents[1]
dotnet = os.environ.get('DOTNET', shutil.which('dotnet') or 'dotnet')
command = ([os.environ['GITLAB_SEARCH_EXE']] if os.environ.get('GITLAB_SEARCH_EXE') else
           [dotnet, str(root / 'src/GitLabSearch/bin/Release/net10.0/GitLabSearch.dll')])
base = os.environ.get('GITLAB_SIM_URL', 'http://127.0.0.1:4900/gitlab')
with tempfile.TemporaryDirectory() as work:
    env = dict(os.environ)
    env.pop('GITLABSEARCH_TOKEN', None)
    def run(*args, code=0):
        p = subprocess.run([*command, *args], cwd=work, env=env, text=True, capture_output=True, timeout=35)
        assert p.returncode == code, (args, p.returncode, p.stderr)
        return p
    run('setup', 'fixture-token', '--api-domain', base, '--concurrency', '8', '--request-delay-ms', '0')
    for term in ['retry-seconds', 'retry-date', 'retry-page']:
        start = time.monotonic()
        p = run(term, '--groups', 'card', '--json')
        data = json.loads(p.stdout)
        assert len(data) == 1 and len(data[0]['matches']) == 2, data
        assert 'retry 1/5' in p.stderr, p.stderr
        assert time.monotonic() - start >= .9, 'Retry-After was not respected'
    p = run('retry-limit', '--groups', 'card', '--json', code=1)
    assert 'retry 5/5' in p.stderr and 'HTTP 429' in p.stderr, p.stderr
    assert not p.stdout.strip(), 'Exhausted retries must not print partial JSON'
    # Long waits must be cancellable; fallback without a header starts at 60 seconds.
    for term, seconds in [('retry-no-header', '60s'), ('retry-cancel', '600s')]:
        p = subprocess.Popen([*command, term, '--groups', 'card', '--json'], cwd=work, env=env,
                             text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        try:
            import select
            ready, _, _ = select.select([p.stderr], [], [], 10)
            assert ready, 'Missing retry diagnostic'
            line = p.stderr.readline()
            assert seconds in line, line
            p.send_signal(signal.SIGINT)
            out, err = p.communicate(timeout=5)
            assert p.returncode == 130 and 'Cancelled.' in err, (p.returncode, err)
            assert not out.strip()
        finally:
            if p.poll() is None: p.kill(); p.wait()
    # A legacy config lacking requestDelayMs uses the new default.
    config = pathlib.Path(work) / '.gitlabsearchrc'
    settings = json.loads(config.read_text())
    settings.pop('requestDelayMs')
    config.write_text(json.dumps(settings))
    start = time.monotonic()
    run('legacy-pacing', '--groups', 'card', '--json')
    assert time.monotonic() - start >= 7, 'Legacy configs should use default pacing'
    # Multiple concurrent workers must share spacing between all HTTP calls.
    settings['requestDelayMs'] = 150
    config.write_text(json.dumps(settings))
    run('global-pacing', '--groups', '1,2', '--json')
    req = urllib.request.Request(base + '/__test/requests', headers={'PRIVATE-TOKEN':'fixture-token'})
    with urllib.request.urlopen(req) as r: requests = json.load(r)
    for index, r in enumerate(requests[:-1]):
        if r.get('status') == 429 and any(t in r['path'] for t in ['retry-seconds', 'retry-date', 'retry-page']):
            assert requests[index + 1]['at'] - r['at'] >= 850, 'Another worker ignored the shared cooldown'
    searches = [r for r in requests if 'search=global-pacing' in r['path']]
    assert len(searches) == 3, searches
    assert all(b['at'] - a['at'] >= 120 for a, b in zip(searches, searches[1:])), searches
print('PASS: 429 seconds/date headers, paginated recovery, bounded retries, cancellation, fallback, legacy defaults and global pacing')
