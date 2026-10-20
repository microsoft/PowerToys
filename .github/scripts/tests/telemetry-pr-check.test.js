const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const test = require('node:test');
const { main } = require('../telemetry-pr-check.js');

const repository = 'microsoft/PowerToys';
const marker = '<!-- telemetry-event-check -->';
const telemetryFile = {
  filename: 'src/runner/trace.cpp',
  status: 'modified',
  patch: '@@ -1 +1 @@\n+TraceLoggingWrite(provider, "NewEvent");',
};

function harness(t, { fork = false, files = [], comments = [], fresh, documentation = '', documentationError = false } = {}) {
  const pull = {
    number: 42,
    state: 'open',
    draft: false,
    user: { login: 'contributor' },
    requested_reviewers: [],
    head: { sha: 'head-sha', repo: { full_name: fork ? 'contributor/PowerToys' : repository } },
    base: { sha: 'base-sha', repo: { full_name: repository } },
  };
  const event = {
    repository: { full_name: repository },
    pull_request: structuredClone(pull),
  };
  const calls = [];
  const logs = [];
  let reads = 0;
  t.mock.method(console, 'log', (...args) => logs.push(args.join(' ')));
  t.mock.method(console, 'warn', (...args) => logs.push(args.join(' ')));
  t.mock.method(global, 'fetch', async (url, options) => {
    assert.ok(options.signal instanceof AbortSignal, 'Every request has a timeout');
    calls.push({ url, method: options.method, body: options.body && JSON.parse(options.body) });
    let result;
    if (options.method !== 'GET') {
      result = { id: 100 };
    } else if (url.endsWith('/pulls/42')) {
      reads += 1;
      result = fresh ? fresh(reads, structuredClone(pull)) : pull;
    } else if (url.includes('/files?')) {
      result = files;
    } else if (url.includes('/comments?')) {
      result = comments;
    } else if (url.includes('/contents/DATA_AND_PRIVACY.md?ref=head-sha')) {
      if (documentationError) return new Response('Unavailable', {
        status: typeof documentationError === 'number' ? documentationError : 503,
      });
      result = { encoding: 'base64', content: Buffer.from(documentation).toString('base64') };
    } else {
      throw new Error(`Unexpected request: ${url}`);
    }
    return new Response(JSON.stringify(result), { status: 200 });
  });
  const previous = { ...process.env };
  process.env.GITHUB_REPOSITORY = repository;
  process.env.GITHUB_TOKEN = 'test-token';
  process.env.GITHUB_API_URL = 'https://api.github.com';
  delete process.env.GITHUB_STEP_SUMMARY;
  t.after(() => {
    process.env = previous;
  });
  return {
    event,
    calls,
    logs,
    setDocumentation: (value) => { documentation = value; },
    writes: () => calls.filter((call) => call.method !== 'GET'),
  };
}

test('fork telemetry is fully analyzed with guidance but no attempted mutation', async (t) => {
  const h = harness(t, { fork: true, files: [telemetryFile] });
  const result = await main(h.event);
  assert.equal(result.required, true);
  assert.deepEqual(h.writes(), []);
  assert.ok(h.logs.some((line) => line.includes('REQUIRED: Added literal')));
  assert.ok(h.logs.some((line) => line.includes('INFO: Telemetry signals')));
  assert.ok(h.calls.some((call) => call.url.includes('/files?')));
});

test('same-repository PR retains guidance comments and reviewer routing', async (t) => {
  const h = harness(t, { documentation: 'NewEvent', files: [
    telemetryFile,
    { filename: 'DATA_AND_PRIVACY.md', status: 'modified', patch: '+NewEvent' },
  ] });
  assert.equal((await main(h.event)).required, false);
  assert.equal(h.writes().length, 2);
  assert.ok(h.writes().some((call) => call.url.endsWith('/requested_reviewers') &&
    call.body.reviewers.includes('chatasweetie')));
  assert.ok(h.writes().some((call) => call.body.body?.includes(marker)));
});

test('no-action succeeds without creating a new comment', async (t) => {
  const h = harness(t, { files: [{ filename: 'README.md', patch: '+Docs' }] });
  assert.equal((await main(h.event)).required, false);
  assert.deepEqual(h.writes(), []);
  assert.ok(h.logs.some((line) => line.includes('REQUIRED: None')));
});

test('concrete event addition without documentation is a required failure', async (t) => {
  const h = harness(t, { files: [telemetryFile] });
  assert.equal((await main(h.event)).required, true);
  assert.equal(h.writes().length, 2);
});

test('path-only and unverifiable follow-up findings remain informational', async (t) => {
  const h = harness(t, { fork: true, files: [
    { filename: 'src/runner/trace.cpp', patch: '+// Refactor telemetry helpers' },
  ] });
  assert.equal((await main(h.event)).required, false);
  assert.deepEqual(h.writes(), []);
});

test('rerun uses fresh files and resolves the previous bot comment', async (t) => {
  const h = harness(t, {
    files: [],
    comments: [{ id: 123, body: `${marker}\nOld required action`,
      user: { login: 'github-actions[bot]', type: 'Bot' } }],
  });
  assert.equal((await main(h.event)).required, false);
  assert.equal(h.writes().length, 1);
  assert.equal(h.writes()[0].method, 'PATCH');
  assert.match(h.writes()[0].body.body, /No telemetry action required/);
});

test('manual dispatch also refuses fork mutations', async (t) => {
  const h = harness(t, { fork: true, files: [telemetryFile] });
  h.event.inputs = { pr_number: '42' };
  delete h.event.pull_request;
  assert.equal((await main(h.event)).required, true);
  assert.deepEqual(h.writes(), []);
});

test('manual dispatch requires matching event, head and base repository identities', async (t) => {
  const h = harness(t, { files: [telemetryFile] });
  h.event.inputs = { pr_number: '42' };
  h.event.repository.full_name = 'another/repository';
  delete h.event.pull_request;
  await main(h.event);
  assert.deepEqual(h.writes(), []);
});

test('stale event head or base never writes', async (t) => {
  const h = harness(t, { files: [telemetryFile] });
  h.event.pull_request.base.sha = 'old-base';
  await assert.rejects(main(h.event), /stale/);
  assert.deepEqual(h.writes(), []);
});

test('head changes while files are fetched discard stale analysis', async (t) => {
  const h = harness(t, { files: [telemetryFile], fresh: (reads, pull) => {
    if (reads > 1) pull.head.sha = 'new-head';
    return pull;
  } });
  await assert.rejects(main(h.event), /rerun required/);
  assert.deepEqual(h.writes(), []);
});

test('repository identity is rechecked immediately before each write', async (t) => {
  const h = harness(t, { files: [telemetryFile], fresh: (reads, pull) => {
    if (reads > 3) pull.head.repo.full_name = 'fork/PowerToys';
    return pull;
  } });
  await main(h.event);
  assert.deepEqual(h.writes(), []);
});

test('draft dispatch skips files and writes', async (t) => {
  const h = harness(t, { fresh: (_reads, pull) => ({ ...pull, draft: true }) });
  delete h.event.pull_request;
  h.event.inputs = { pr_number: '42' };
  await main(h.event);
  assert.deepEqual(h.writes(), []);
  assert.equal(h.calls.length, 1);
});

test('a contributor cannot spoof the canonical bot comment', async (t) => {
  const h = harness(t, {
    files: [telemetryFile],
    comments: [{ id: 999, body: marker, user: { login: 'contributor', type: 'User' } }],
  });
  await main(h.event);
  assert.ok(!h.writes().some((call) => call.method === 'PATCH'));
});

test('moved event calls, source comments and documentation examples are not concrete failures', async (t) => {
  const h = harness(t, { fork: true, files: [
    { filename: 'src/old.cpp', patch: '-TraceLoggingWrite(provider, "MovedEvent");' },
    { filename: 'src/new.cpp', patch: '+TraceLoggingWrite(provider, "MovedEvent");' },
    { filename: 'src/trace.cpp', patch: '+// TraceLoggingWrite(provider, "Example");' },
    { filename: 'README.md', patch: '+TraceLoggingWrite(provider, "Example");' },
  ] });
  assert.equal((await main(h.event)).required, false);
  assert.deepEqual(h.writes(), []);
});

test('removing the privacy document does not satisfy the existing documentation requirement', async (t) => {
  const h = harness(t, { fork: true, files: [
    telemetryFile,
    { filename: 'DATA_AND_PRIVACY.md', status: 'removed', patch: '-Old docs' },
  ] });
  assert.equal((await main(h.event)).required, true);
});

test('resolved documentation rerun updates guidance and succeeds', async (t) => {
  const h = harness(t, { documentation: 'NewEvent', files: [
    telemetryFile,
    { filename: 'DATA_AND_PRIVACY.md', status: 'modified', patch: '+New event' },
  ], comments: [
    { id: 123, body: `${marker}\nMissing documentation`,
      user: { login: 'github-actions[bot]', type: 'Bot' } },
  ] });
  assert.equal((await main(h.event)).required, false);
  const commentWrite = h.writes().find((call) => call.method === 'PATCH');
  assert.match(commentWrite.body.body, /After this PR is merged/);
  assert.doesNotMatch(commentWrite.body.body, /Add your telemetry events/);
});

test('an already documented event call is informational, not a policy failure', async (t) => {
  const h = harness(t, {
    fork: true, files: [telemetryFile],
    documentation: '| Microsoft.PowerToys.NewEvent | Existing event description |',
  });
  assert.equal((await main(h.event)).required, false);
  assert.deepEqual(h.writes(), []);
});

test('unverifiable documentation is informational rather than an invented policy failure', async (t) => {
  const h = harness(t, { fork: true, files: [telemetryFile], documentationError: true });
  assert.equal((await main(h.event)).required, false);
  assert.ok(h.logs.some((line) => line.includes('Documentation could not be verified')));
  assert.deepEqual(h.writes(), []);
});

test('missing head documentation cannot clear a concrete required omission', async (t) => {
  const h = harness(t, { fork: true, files: [telemetryFile], documentationError: 404 });
  assert.equal((await main(h.event)).required, true);
  assert.deepEqual(h.writes(), []);
});

test('summary includes required versus informational findings even on no-action', async (t) => {
  const h = harness(t);
  const summaries = [];
  process.env.GITHUB_STEP_SUMMARY = 'summary-fixture.md';
  t.mock.method(fs, 'appendFileSync', (filename, text) => {
    assert.equal(filename, 'summary-fixture.md');
    summaries.push(text);
  });
  await main(h.event);
  assert.equal(summaries.length, 1);
  assert.match(summaries[0], /REQUIRED: None automatically verified/);
  assert.match(summaries[0], /INFO: No telemetry signals remain/);
  assert.deepEqual(h.writes(), []);
});

test('same-repository heuristic signals never post a falsely mandatory documentation instruction', async (t) => {
  const h = harness(t, {
    files: [{ filename: 'src/runner/trace.cpp', patch: '+// helper formatting' }],
  });
  assert.equal((await main(h.event)).required, false);
  const comment = h.writes().find((call) => call.body.body);
  assert.match(comment.body.body, /signals alone do not establish an unmet requirement/);
  assert.doesNotMatch(comment.body.body, /Add your telemetry events/);
});

test('manual same-repository dispatch preserves useful writes', async (t) => {
  const h = harness(t, { files: [telemetryFile] });
  delete h.event.pull_request;
  h.event.inputs = { pr_number: '42' };
  assert.equal((await main(h.event)).required, true);
  assert.equal(h.writes().length, 2);
});

test('a fetched foreign base repository is read-only even with a same-repository head', async (t) => {
  const h = harness(t, { files: [telemetryFile], fresh: (_reads, pull) => {
    pull.base.repo.full_name = 'another/repository';
    return pull;
  } });
  await main(h.event);
  assert.deepEqual(h.writes(), []);
});

test('changes during documentation verification cannot produce stale required failures', async (t) => {
  const h = harness(t, { files: [telemetryFile], fresh: (reads, pull) => {
    if (reads > 2) pull.head.sha = 'updated-after-document-read';
    return pull;
  } });
  await assert.rejects(main(h.event), /rerun required/);
  assert.deepEqual(h.writes(), []);
});

test('CLI returns failure only for verified missing documentation, and clears on a resolved rerun', () => {
  const pull = {
    number: 42, state: 'open', draft: false,
    head: { sha: 'head-sha', repo: { full_name: 'fork/PowerToys' } },
    base: { sha: 'base-sha', repo: { full_name: repository } },
  };
  for (const [files, documentation, expectedExit, stale = false] of [
    [[telemetryFile], '', 1],
    [[telemetryFile], 'Microsoft.PowerToys.NewEvent', 0],
    [[], '', 0],
    [[telemetryFile], '', 1, true],
  ]) {
    const event = { repository: { full_name: repository }, pull_request: structuredClone(pull) };
    if (stale) event.pull_request.head.sha = 'stale';
    const preload = `
      import fs from 'node:fs';
      const read = fs.readFileSync;
      fs.readFileSync = (filename, ...args) => filename === 'event-fixture'
        ? ${JSON.stringify(JSON.stringify(event))} : read(filename, ...args);
      globalThis.fetch = async (url, options) => {
        if (options.method !== 'GET') throw new Error('Fork attempted mutation');
        let value;
        if (url.endsWith('/pulls/42')) value = ${JSON.stringify(pull)};
        else if (url.includes('/files?')) value = ${JSON.stringify(files)};
        else if (url.includes('/contents/')) value = {
          encoding: 'base64', content: ${JSON.stringify(Buffer.from(documentation).toString('base64'))}
        };
        else throw new Error('Unexpected request: ' + url);
        return new Response(JSON.stringify(value), { status: 200 });
      };
    `;
    const env = {
      ...process.env,
      GITHUB_EVENT_PATH: 'event-fixture',
      GITHUB_REPOSITORY: repository,
      GITHUB_TOKEN: 'test-token',
      GITHUB_API_URL: 'https://api.github.com',
    };
    delete env.GITHUB_STEP_SUMMARY;
    const result = spawnSync(process.execPath, [
      '--import', `data:text/javascript,${encodeURIComponent(preload)}`,
      path.join(__dirname, '..', 'telemetry-pr-check.js'),
    ], { env, encoding: 'utf8', timeout: 30000 });
    assert.equal(result.error, undefined);
    assert.equal(result.status, expectedExit, result.stdout + result.stderr);
    assert.match(result.stdout, /REQUIRED:/);
    assert.match(result.stdout, stale ? /ERROR: Analysis incomplete/ : /INFO:/);
  }
});

test('literal event examples in test code are informational rather than a production policy violation', async (t) => {
  const h = harness(t, { fork: true, files: [
    { ...telemetryFile, filename: 'src/runner/UnitTests/trace.cpp' },
  ] });
  assert.equal((await main(h.event)).required, false);
  assert.deepEqual(h.writes(), []);
});

test('case-variant lookalike privacy filenames cannot satisfy the existing documentation requirement', async (t) => {
  const h = harness(t, { fork: true, files: [
    telemetryFile,
    { filename: 'data_and_privacy.md', status: 'added', patch: '+NewEvent' },
  ] });
  assert.equal((await main(h.event)).required, true);
});

test('normal multiline literal event calls remain required until documented', async (t) => {
  const h = harness(t, { fork: true, files: [{
    ...telemetryFile,
    patch: '@@ -1 +1,4 @@\n+TraceLoggingWrite(\n+    provider,\n+    "NewEvent",\n+    TraceLoggingInt32(value, "Value"));',
  }] });
  assert.equal((await main(h.event)).required, true);
  assert.deepEqual(h.writes(), []);
});

test('an unrelated privacy edit and substring occurrence do not resolve an omission', async (t) => {
  const h = harness(t, { fork: true, documentation: 'OtherNewEventSuffix',
    files: [telemetryFile, { filename: 'DATA_AND_PRIVACY.md', status: 'modified', patch: '+Typo fix' }],
  });
  assert.equal((await main(h.event)).required, true);
  assert.deepEqual(h.writes(), []);
});

test('unchanged unresolved telemetry reruns keep failing, then fresh head documentation resolves', async (t) => {
  const h = harness(t, { fork: true, files: [telemetryFile] });
  assert.equal((await main(h.event)).required, true);
  assert.equal((await main(h.event)).required, true);
  assert.deepEqual(h.writes(), []);
  assert.equal(h.calls.filter((call) => call.url.includes('/contents/')).length, 2);
  h.setDocumentation('| Microsoft.PowerToys.NewEvent | Event description |');
  assert.equal((await main(h.event)).required, false);
  assert.deepEqual(h.writes(), []);
});
