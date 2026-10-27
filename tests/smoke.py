"""Run against the committed Postman simulation; no real GitLab credentials needed."""
import json, os, pathlib, shutil, subprocess, tempfile

root = pathlib.Path(__file__).resolve().parents[1]
dotnet = os.environ.get('DOTNET', shutil.which('dotnet') or 'dotnet')
dll = root / 'src/GitLabSearch/bin/Release/net10.0/GitLabSearch.dll'
base = os.environ.get('GITLAB_SIM_URL', 'http://127.0.0.1:4900/gitlab')
with tempfile.TemporaryDirectory() as work:
    env = dict(os.environ)
    env.pop('GITLABSEARCH_TOKEN', None)
    def run(*args, code=0):
        p = subprocess.run([dotnet, str(dll), *args], cwd=work, env=env, text=True, capture_output=True)
        assert p.returncode == code, (args, p.returncode, p.stderr)
        return p
    assert 'Search options' in run('--help').stdout
    assert run('--version').stdout.strip() == '2.0.1'
    run('setup', 'fixture-token', '--api-domain', base, '--concurrency', '2')
    config = pathlib.Path(work) / '.gitlabsearchrc'
    assert json.loads(config.read_text())['token'] == 'fixture-token'
    if os.name != 'nt': assert config.stat().st_mode & 0o777 == 0o600
    result = json.loads(run('hello', '--json').stdout)
    assert len(result) == 1 and len(result[0]['matches']) == 2, result
    nested = json.loads(run('hello', '--groups', 'card', '--json').stdout)
    assert len(nested) == 1 and len(nested[0]['matches']) == 2, nested
    term = 'a+b & فارسی'
    result = json.loads(run(term, '-g', 'team/sub', '-f', '*.cs', '-e', 'cs', '-p', 'src', '--json').stdout)
    assert result[0]['matches'][0]['data'] == term + ' filename:*.cs extension:cs path:src'
    assert json.loads(run('hello', '--archive', 'only', '--json').stdout) == []
    assert len(json.loads(run('hello', '--archive', 'exclude', '--json').stdout)) == 1
    assert 'First%20file.cs#L3' in run('hello').stdout
    assert 'HTTP 403' in run('error', code=1).stderr
    assert 'unsafe pagination' in run('unsafe', code=1).stderr
    env['GITLABSEARCH_TOKEN'] = 'wrong-token'
    assert 'HTTP 401' in run('hello', code=1).stderr
    assert 'wrong-token' not in run('hello', code=1).stderr
    env.pop('GITLABSEARCH_TOKEN')
    run('hello', '--archive', 'invalid', code=1)
    run('setup', 'token', '--concurrency', '0', code=1)
    run('--unknown', code=1)
    config.write_text('{invalid')
    assert 'Invalid JSON' in run('hello', code=1).stderr
print('PASS: CLI, setup, permissions, pagination, deduplication, filters, encoding, archive, URLs, auth, errors and validation')
