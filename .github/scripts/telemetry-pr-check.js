#!/usr/bin/env node

/**
 * Detects telemetry-event additions/modifications in a pull request and
 * posts (or updates) a PR comment when telemetry-related changes are found.
 *
 * This script is executed by .github/workflows/telemetry-pr-check.yml.
 * Keep both files aligned when changing trigger behavior, env usage, or messaging.
 */

const fs = require('node:fs');

const COMMENT_MARKER = '<!-- telemetry-event-check -->';
const COMMENT_BODY_WITH_PRIVACY_UPDATE = `${COMMENT_MARKER}
THIS IS A TEST | @chatasweetie is testing this functionality
Thanks for contributing to PowerToys. This change might include a new or modified telemetry event, and we want to help make sure you can get your data end to end.

1. Reach out to Jessica (@chatasweetie) to follow up on the next steps to add these telemetry events to our pipelines.`;

const COMMENT_BODY_WITHOUT_PRIVACY_UPDATE = `${COMMENT_MARKER}
THIS IS A TEST | @chatasweetie is testing this functionality
Thanks for contributing to PowerToys. This change might include a new or modified telemetry event, and we want to help make sure you can get your data end to end.

1. Make sure to add your telemetry events to DATA_AND_PRIVACY.md.

2. Reach out to Jessica (@chatasweetie) to follow up on the next steps to add these telemetry events to our pipelines.`;

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
  const response = await fetch(url, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      Accept: 'application/vnd.github+json',
      'Content-Type': 'application/json',
    },
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`${method} ${url} failed (${response.status}): ${text}`);
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

async function getAllPullFiles(apiBaseUrl, repository, pullNumber) {
  const files = [];
  let page = 1;

  while (true) {
    const url = `${apiBaseUrl}/repos/${repository}/pulls/${pullNumber}/files?per_page=100&page=${page}`;
    const batch = await apiRequest(url);
    if (!Array.isArray(batch) || batch.length === 0) {
      break;
    }

    files.push(...batch);

    if (batch.length < 100) {
      break;
    }

    page += 1;
  }

  return files;
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
    const filename = (file.filename || '').toLowerCase();
    return filename === 'data_and_privacy.md';
  });
}

async function upsertPrComment(apiBaseUrl, repository, pullNumber, body) {
  const commentsUrl = `${apiBaseUrl}/repos/${repository}/issues/${pullNumber}/comments?per_page=100`;
  const comments = await apiRequest(commentsUrl);
  const existing = (comments || []).find((comment) =>
    typeof comment.body === 'string' && comment.body.includes(COMMENT_MARKER)
  );

  if (existing) {
    const updateUrl = `${apiBaseUrl}/repos/${repository}/issues/comments/${existing.id}`;
    await apiRequest(updateUrl, 'PATCH', { body });
    console.log(`Updated existing telemetry comment (id: ${existing.id}).`);
    return;
  }

  const createUrl = `${apiBaseUrl}/repos/${repository}/issues/${pullNumber}/comments`;
  await apiRequest(createUrl, 'POST', { body });
  console.log('Created telemetry comment on PR.');
}

async function main() {
  const eventPath = requireEnv('GITHUB_EVENT_PATH');
  const repository = requireEnv('GITHUB_REPOSITORY');
  const apiBaseUrl = process.env.GITHUB_API_URL || 'https://api.github.com';

  const event = JSON.parse(fs.readFileSync(eventPath, 'utf8'));
  const pullNumber = event?.pull_request?.number;

  if (!pullNumber) {
    console.log('No pull_request payload found; skipping.');
    return;
  }

  const files = await getAllPullFiles(apiBaseUrl, repository, pullNumber);
  const matches = detectTelemetryChanges(files);
  const dataAndPrivacyChanged = hasDataAndPrivacyChange(files);

  console.log(`Scanned ${files.length} changed files.`);
  console.log(`Telemetry matches found: ${matches.length}.`);
  console.log(`DATA_AND_PRIVACY.md changed: ${dataAndPrivacyChanged}.`);

  if (matches.length === 0) {
    console.log('No telemetry-related additions/modifications detected.');
    return;
  }

  for (const match of matches) {
    console.log(
      `- ${match.filename} (telemetryPath=${match.telemetryPath}, telemetryLineSignal=${match.telemetryLineSignal}, patchUnavailable=${match.patchUnavailable})`
    );
  }

  const commentBody = dataAndPrivacyChanged
    ? COMMENT_BODY_WITH_PRIVACY_UPDATE
    : COMMENT_BODY_WITHOUT_PRIVACY_UPDATE;

  await upsertPrComment(apiBaseUrl, repository, pullNumber, commentBody);
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
