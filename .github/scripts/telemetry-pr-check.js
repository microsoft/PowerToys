#!/usr/bin/env node

/**
 * Analyzes PR diff data without executing PR code. Forks receive read-only
 * findings; same-repository PRs also receive guidance and reviewer routing.
 * Only concrete documentation omissions fail the check, not telemetry signals.
 *
 * This script is executed by .github/workflows/telemetry-pr-check.yml.
 * Keep both files aligned when changing trigger behavior, env usage, or messaging.
 */

const fs = require('node:fs');
const REVIEWER_LOGIN = 'chatasweetie';
const REVIEWER_MENTION = `@${REVIEWER_LOGIN}`;

const COMMENT_MARKER = '<!-- telemetry-event-check -->';
const COMMENT_BODY_WITH_PRIVACY_UPDATE = `${COMMENT_MARKER}
Thank you for contributing to PowerToys. We've detected that this PR might include a new or modified telemetry event. After this PR is merged, please follow these next steps:

- [ ] Reach out to Jessica (${REVIEWER_MENTION}) to follow up on the next steps: https://aka.ms/pt-telemetry-process
`;

const COMMENT_BODY_WITHOUT_PRIVACY_UPDATE = `${COMMENT_MARKER}
Thank you for contributing to PowerToys. We've detected that this PR might include a new or modified telemetry event. Please ensure the following before merging:

- [ ] Add your telemetry events to [DATA_AND_PRIVACY.md](https://github.com/microsoft/PowerToys/blob/main/DATA_AND_PRIVACY.md) within this PR.

- [ ] Reach out to Jessica (${REVIEWER_MENTION}) to follow up on the next steps: https://aka.ms/pt-telemetry-process`;

const COMMENT_BODY_INFORMATIONAL = `${COMMENT_MARKER}
This PR contains possible telemetry changes. These signals alone do not establish an unmet requirement.

If you are adding or modifying telemetry events, check whether [DATA_AND_PRIVACY.md](https://github.com/microsoft/PowerToys/blob/main/DATA_AND_PRIVACY.md) needs updating. Existing documentation could already cover these events; path-only matches and unavailable patches need human review.

For telemetry process follow-up, contact Jessica (${REVIEWER_MENTION}): https://aka.ms/pt-telemetry-process`;

const TELEMETRY_PATH_PATTERNS = [
  /(^|\/)trace\.(h|hpp|cpp|cs)$/i,
  /(^|\/)telemetry\//i,
  /(^|\/)events\/.+event\.cs$/i,
  /^src\/common\/Telemetry\//i,
  /^src\/common\/ManagedTelemetry\//i,
  /^src\/runner\/trace\.(h|cpp)$/i,
  /^src\/settings-ui\/.+\/Telemetry\//i,
];

const TELEMETRY_LINE_PATTERNS = [
  /TraceLoggingWriteWrapper\s*\(/,
  /\bTraceLoggingWrite\s*\(/,
  /\bTRACELOGGING_DEFINE_PROVIDER\b/,
  /\bTraceLoggingOptionProjectTelemetry\b/,
  /\bProjectTelemetryPrivacyDataTag\b/,
  /\bPROJECT_KEYWORD_MEASURE\b/,
  /\bRegisterProvider\s*\(/,
  /\bUnregisterProvider\s*\(/,
  /\bPowerToysTelemetry\.Log\.WriteEvent\s*\(/,
  /\bclass\s+\w+\s*:\s*EventBase\s*,\s*IEvent\b/,
  /\bclass\s+\w+\s*:\s*TelemetryBase\b/,
  /\bPartA_PrivTags\b/,
  /\[EventData\]/,
  /\bEventName\b/,
];

function requireEnv(name) {
  const value = process.env[name];
  if (!value) {
    throw new Error(`Missing required environment variable: ${name}`);
  }
  return value;
}

function validateRepository(repository) {
  if (!/^[^/]+\/[^/]+$/.test(repository)) {
    throw new Error(
      `GITHUB_REPOSITORY must be in owner/repo format, received: ${JSON.stringify(repository)}`
    );
  }
}

function readEventPayload(eventPath) {
  let raw;
  try {
    raw = fs.readFileSync(eventPath, 'utf8');
  } catch (error) {
    throw new Error(`Failed to read event payload at ${eventPath}: ${error.message}`);
  }

  try {
    return JSON.parse(raw);
  } catch (error) {
    throw new Error(`Failed to parse JSON from ${eventPath}: ${error.message}`);
  }
}

function resolvePullNumber(event) {
  const fromPullRequest = event?.pull_request?.number;
  const fromWorkflowDispatch = event?.inputs?.pr_number;
  const rawPullNumber = fromPullRequest ?? fromWorkflowDispatch;

  if (rawPullNumber === undefined || rawPullNumber === null || rawPullNumber === '') {
    throw new Error(
      'Unable to determine pull request number from event payload. Expected pull_request.number or inputs.pr_number.'
    );
  }

  const pullNumber = /^\d+$/.test(String(rawPullNumber)) ? Number(rawPullNumber) : NaN;
  if (!Number.isInteger(pullNumber) || pullNumber <= 0) {
    throw new Error(`Invalid pull request number: ${JSON.stringify(rawPullNumber)}`);
  }

  return pullNumber;
}

function isTelemetryPath(filePath) {
  return TELEMETRY_PATH_PATTERNS.some((pattern) => pattern.test(filePath));
}

function changedLinesFromPatch(patch) {
  if (!patch) {
    return [];
  }

  return patch
    .split('\n')
    .filter((line) => {
      if (line.startsWith('+++') || line.startsWith('---')) {
        return false;
      }
      return line.startsWith('+') || line.startsWith('-');
    })
    .map((line) => line.slice(1));
}

function hasTelemetryLineSignal(lines) {
  return lines.some((line) => TELEMETRY_LINE_PATTERNS.some((pattern) => pattern.test(line)));
}

async function apiRequest(url, method = 'GET', body) {
  const token = requireEnv('GITHUB_TOKEN');
  let response;
  try {
    response = await fetch(url, {
      method,
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: 'application/vnd.github+json',
        'Content-Type': 'application/json',
      },
      body: body ? JSON.stringify(body) : undefined,
      signal: AbortSignal.timeout(30000),
    });
  } catch (error) {
    throw new Error(`Network error during ${method} ${url}: ${error.message}`);
  }

  if (!response.ok) {
    const text = await response.text();
    const rateLimitReset = response.headers.get('x-ratelimit-reset');
    const rateLimitHint =
      response.status === 403 && rateLimitReset
        ? ` (rate limit reset at epoch ${rateLimitReset})`
        : '';
    const error = new Error(`${method} ${url} failed (${response.status})${rateLimitHint}: ${text}`);
    error.status = response.status;
    throw error;
  }

  if (response.status === 204) {
    return null;
  }

  try {
    return await response.json();
  } catch (error) {
    throw new Error(`Failed to parse JSON response for ${method} ${url}: ${error.message}`);
  }
}

async function getAllPullFiles(apiBaseUrl, repository, pullNumber) {
  const files = [];
  let page = 1;

  while (page <= 30) {
    const url = `${apiBaseUrl}/repos/${repository}/pulls/${pullNumber}/files?per_page=100&page=${page}`;
    const batch = await apiRequest(url);
    if (!Array.isArray(batch)) {
      throw new Error(`Unexpected response while listing PR files on page ${page}.`);
    }

    if (batch.length === 0) {
      break;
    }

    files.push(...batch);

    if (batch.length < 100) {
      break;
    }

    page += 1;
  }

  if (page > 30) {
    throw new Error('PR files exceeded the 3000-file analysis limit.');
  }
  return files;
}

async function getPullRequest(apiBaseUrl, repository, pullNumber) {
  const url = `${apiBaseUrl}/repos/${repository}/pulls/${pullNumber}`;
  const pullRequest = await apiRequest(url);
  if (!pullRequest || typeof pullRequest !== 'object') {
    throw new Error('Unexpected response while fetching pull request details.');
  }
  return pullRequest;
}

async function ensureReviewerRequested(apiBaseUrl, repository, pullNumber, pullRequest, write) {
  const authorLogin = String(pullRequest?.user?.login || '').toLowerCase();
  const targetReviewer = REVIEWER_LOGIN.toLowerCase();

  if (authorLogin === targetReviewer) {
    console.log(`Skipping reviewer request: ${REVIEWER_LOGIN} is the PR author.`);
    return;
  }

  const requestedReviewers = Array.isArray(pullRequest?.requested_reviewers)
    ? pullRequest.requested_reviewers
    : [];
  const alreadyRequested = requestedReviewers.some(
    (reviewer) => String(reviewer?.login || '').toLowerCase() === targetReviewer
  );

  if (alreadyRequested) {
    console.log(`Reviewer ${REVIEWER_LOGIN} is already requested.`);
    return;
  }

  const url = `${apiBaseUrl}/repos/${repository}/pulls/${pullNumber}/requested_reviewers`;
  try {
    await write(url, 'POST', { reviewers: [REVIEWER_LOGIN] });
    console.log(`Requested reviewer ${REVIEWER_LOGIN}.`);
  } catch (error) {
    // Reviewer request should not fail the telemetry guidance workflow.
    console.warn(
      `Unable to request reviewer ${REVIEWER_LOGIN}: ${error instanceof Error ? error.message : String(error)}`
    );
  }
}

async function findExistingTelemetryComment(apiBaseUrl, repository, pullNumber) {
  let page = 1;

  while (page <= 20) {
    const commentsUrl = `${apiBaseUrl}/repos/${repository}/issues/${pullNumber}/comments?per_page=100&page=${page}`;
    const comments = await apiRequest(commentsUrl);

    if (!Array.isArray(comments)) {
      throw new Error(`Unexpected response while listing issue comments on page ${page}.`);
    }

    const existing = comments.find(
      (comment) => comment.user?.login === 'github-actions[bot]' &&
        comment.user?.type === 'Bot' &&
        typeof comment.body === 'string' && comment.body.includes(COMMENT_MARKER)
    );
    if (existing) {
      return existing;
    }

    if (comments.length < 100) {
      return null;
    }

    page += 1;
  }
  throw new Error('PR comments exceeded the 2000-comment analysis limit.');
}

function detectTelemetryChanges(files) {
  const matches = [];

  for (const file of files) {
    const filename = file.filename || '';
    const telemetryPath = isTelemetryPath(filename);
    const changedLines = changedLinesFromPatch(file.patch);
    const telemetryLineSignal = hasTelemetryLineSignal(changedLines);

    // Some large diffs omit patch content. If the file path is telemetry-centric,
    // treat it as a telemetry modification to avoid false negatives.
    const patchUnavailable = !file.patch && telemetryPath;

    if (telemetryPath || telemetryLineSignal || patchUnavailable) {
      matches.push({
        filename,
        telemetryPath,
        telemetryLineSignal,
        patchUnavailable,
      });
    }
  }

  return matches;
}

function hasDataAndPrivacyChange(files) {
  return files.some((file) => {
    return file.filename === 'DATA_AND_PRIVACY.md' && file.status !== 'removed';
  });
}

async function upsertPrComment(apiBaseUrl, repository, pullNumber, body, write, create = true) {
  const existing = await findExistingTelemetryComment(apiBaseUrl, repository, pullNumber);

  if (existing) {
    if (existing.body === body) {
      return;
    }
    const updateUrl = `${apiBaseUrl}/repos/${repository}/issues/comments/${existing.id}`;
    await write(updateUrl, 'PATCH', { body });
    console.log(`Updated existing telemetry comment (id: ${existing.id}).`);
    return;
  }

  if (!create) {
    return;
  }
  const createUrl = `${apiBaseUrl}/repos/${repository}/issues/${pullNumber}/comments`;
  await write(createUrl, 'POST', { body });
  console.log('Created telemetry comment on PR.');
}

function report(lines) {
  if (!lines.some((line) => line.startsWith('REQUIRED:'))) {
    lines.push('REQUIRED: None evaluated; analysis skipped or incomplete.');
  }
  const text = ['## Telemetry PR check', ...lines, ''].join('\n');
  console.log(text);
  if (process.env.GITHUB_STEP_SUMMARY) {
    fs.appendFileSync(process.env.GITHUB_STEP_SUMMARY, `${text}\n`);
  }
}

function sameRepository(pullRequest, repository, event) {
  return pullRequest.head?.repo?.full_name === repository &&
    pullRequest.base?.repo?.full_name === repository &&
    event.repository?.full_name === repository;
}

function sameRevision(left, right) {
  return Boolean(left.head?.sha && left.base?.sha) &&
    left.head.sha === right.head?.sha && left.base.sha === right.base?.sha;
}

// Only added literal event calls are concrete evidence. Path matches and other
// telemetry signals alone cannot prove that the documentation policy applies.
function concreteAddedEventNames(files) {
  const eventCall = /(?:^|\n)\s*TraceLoggingWrite(?:Wrapper)?\s*\(\s*(?:\w+\s*,\s*)?"([^"]+)"/g;
  const added = new Set();
  const removed = new Set();
  for (const file of files) {
    if (!String(file.filename || '').startsWith('src/') ||
        /(?:^|\/)[^/]*(?:test|mock)[^/]*(?:\/|$)/i.test(file.filename)) continue;
    if (!/\.(?:c|cpp|cxx|h|hpp|cs)$/i.test(file.filename || '')) continue;
    for (const [prefix, names] of [['+', added], ['-', removed]]) {
      // Context lines allow normally formatted multiline calls, while excluding
      // opposite-side lines keeps the before/after versions separate.
      let changed = false;
      const source = (file.patch || '').split('\n').map((line) => {
        if (line.startsWith('@@')) return '\n';
        if (line.startsWith(prefix)) { changed = true; return line.slice(1); }
        return line.startsWith(' ') ? line.slice(1) : '';
      }).join('\n');
      if (changed) {
        for (const match of source.matchAll(eventCall)) names.add(match[1]);
      }
    }
  }
  return [...added].filter((name) => !removed.has(name));
}

async function undocumentedAddedEvents(apiBaseUrl, repository, headSha, files) {
  const names = concreteAddedEventNames(files);
  if (!names.length) return [];
  const url = `${apiBaseUrl}/repos/${repository}/contents/DATA_AND_PRIVACY.md?ref=${encodeURIComponent(headSha)}`;
  let document;
  try {
    document = await apiRequest(url);
  } catch (error) {
    if (error.status === 404) return names;
    throw error;
  }
  if (document?.encoding !== 'base64' || typeof document.content !== 'string' ||
      document.content.length > 2000000) {
    throw new Error('The existing privacy document could not be verified.');
  }
  const text = Buffer.from(document.content, 'base64').toString('utf8');
  const documented = new Set(text.match(/[A-Za-z_][A-Za-z0-9_.-]*/g) || []);
  return names.filter((name) => ![...documented].some((entry) =>
    entry === name || entry.endsWith(`.${name}`)));
}

async function main(event = readEventPayload(requireEnv('GITHUB_EVENT_PATH'))) {
  const repository = requireEnv('GITHUB_REPOSITORY');
  const apiBaseUrl = process.env.GITHUB_API_URL || 'https://api.github.com';
  validateRepository(repository);

  try {
    new URL(apiBaseUrl);
  } catch {
    throw new Error(`Invalid GITHUB_API_URL: ${JSON.stringify(apiBaseUrl)}`);
  }

  const pullNumber = resolvePullNumber(event);

  console.log(`Event name: ${process.env.GITHUB_EVENT_NAME || 'unknown'}`);
  console.log(`Repository: ${repository}`);
  console.log(`PR number: ${pullNumber}`);

  const initial = await getPullRequest(apiBaseUrl, repository, pullNumber);
  if (initial.draft || initial.state !== 'open') {
    report(['INFO: PR is draft or closed; no action required.']);
    return;
  }
  if (event.pull_request && !sameRevision(initial, event.pull_request)) {
    throw new Error('Event head/base is stale; rerun required. No mutations performed.');
  }
  const files = await getAllPullFiles(apiBaseUrl, repository, pullNumber);
  const current = await getPullRequest(apiBaseUrl, repository, pullNumber);
  if (!sameRevision(initial, current) || current.draft || current.state !== 'open') {
    throw new Error('PR head/base or readiness changed during analysis; rerun required.');
  }

  const matches = detectTelemetryChanges(files);
  const dataAndPrivacyChanged = hasDataAndPrivacyChange(files);

  console.log(`Scanned ${files.length} changed files.`);
  console.log(`Telemetry matches found: ${matches.length}.`);
  console.log(`DATA_AND_PRIVACY.md changed: ${dataAndPrivacyChanged}.`);

  let missingEvents = [];
  let documentationInfo = '';
  try {
    missingEvents = await undocumentedAddedEvents(apiBaseUrl,
      current.head.repo.full_name, current.head.sha, files);
  } catch (error) {
    documentationInfo = `INFO: Documentation could not be verified; review manually: ${error.message}`;
  }
  const verified = await getPullRequest(apiBaseUrl, repository, pullNumber);
  if (!sameRevision(current, verified) || verified.draft || verified.state !== 'open' ||
      current.head.repo?.full_name !== verified.head.repo?.full_name ||
      current.base.repo?.full_name !== verified.base.repo?.full_name) {
    throw new Error('PR changed during documentation verification; rerun required.');
  }
  const required = missingEvents.length > 0;
  const canWrite = sameRepository(current, repository, event);
  report([
    `Mode: ${canWrite ? 'same-repository guidance' : 'read-only; no mutations attempted'}.`,
    `Head: ${current.head.sha}; base: ${current.base.sha}.`,
    `Scanned ${files.length} files; telemetry signals: ${matches.length}.`,
    required
      ? `REQUIRED: Added literal telemetry event ${missingEvents.join(', ')} is absent from DATA_AND_PRIVACY.md at the PR head. Add the event documentation within this PR.`
      : 'REQUIRED: None automatically verified.',
    matches.length
      ? 'INFO: Telemetry signals need review; contact @chatasweetie for follow-up. Signals and follow-up alone do not fail this check.'
      : 'INFO: No telemetry signals remain.',
    ...matches.map((match) => `INFO: ${JSON.stringify(match.filename)}`),
    ...(documentationInfo ? [documentationInfo] : []),
  ]);

  const write = async (url, method, body) => {
    const fresh = await getPullRequest(apiBaseUrl, repository, pullNumber);
    if (!sameRepository(fresh, repository, event) || !sameRevision(current, fresh) ||
        fresh.draft || fresh.state !== 'open') {
      throw new Error('PR identity, head/base or readiness changed; refusing mutation.');
    }
    return apiRequest(url, method, body);
  };

  if (canWrite) {
    try {
      if (matches.length) {
        await ensureReviewerRequested(apiBaseUrl, repository, pullNumber, current, write);
      }
      const commentBody = matches.length
        ? (required ? COMMENT_BODY_WITHOUT_PRIVACY_UPDATE :
          (dataAndPrivacyChanged ? COMMENT_BODY_WITH_PRIVACY_UPDATE : COMMENT_BODY_INFORMATIONAL))
        : `${COMMENT_MARKER}\nTelemetry signals from the earlier run are no longer present. No telemetry action required.`;
      await upsertPrComment(apiBaseUrl, repository, pullNumber, commentBody, write, matches.length > 0);
    } catch (error) {
      report([`INFO: Guidance could not be updated: ${error.message}`]);
    }
  }
  return { required };
}

if (require.main === module) {
  main().then((result) => {
    if (result?.required) {
      process.exitCode = 1;
    }
  }).catch((error) => {
    report([`ERROR: Analysis incomplete: ${error.message}`]);
    process.exitCode = 1;
  });
}

module.exports = { main };
