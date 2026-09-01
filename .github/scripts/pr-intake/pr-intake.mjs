import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const CANONICAL_MARKER = '<!-- powertoys-pr-intake:canonical:v1 -->';
export const ACTIONS_BOT_LOGIN = 'github-actions[bot]';
export const ACTIONS_BOT_ID = 41898282;
export const READY_FOR_REVIEW_LABEL = 'Ready for review';
export const NEEDS_AUTHOR_FEEDBACK_LABEL = 'Needs-Author-Feedback';
const LEGACY_READY_FOR_REVIEW_LABEL = 'Needs-Review';
export const FEEDBACK_SINCE_MARKER = 'powertoys-pr-intake:feedback-since';
export const STALE_FEEDBACK_DAYS = 7;

const PAGE_SIZE = 100;
const MAX_COMMENT_PAGES = 10;
const MAX_FILE_PAGES = 10;
// Closing references come from the untrusted PR body. Cap how many we accept and
// verify them with bounded concurrency so a crafted body cannot fan out into
// thousands of concurrent API calls and exhaust the token's rate limit.
const MAX_CLOSING_REFERENCES = 20;
const CLOSING_VERIFY_CONCURRENCY = 5;
// GitHub computes mergeability asynchronously and returns `mergeable: null`
// meanwhile. Re-fetch a few times before treating the state as known.
const MERGEABILITY_MAX_ATTEMPTS = 5;
const MERGEABILITY_RETRY_DELAY_MS = 2000;

const VISUAL_FILE_EXTENSIONS = new Set([
  '.axaml',
  '.css',
  '.gif',
  '.html',
  '.ico',
  '.jpeg',
  '.jpg',
  '.png',
  '.svg',
  '.webp',
  '.xaml',
]);

const VISUAL_PRODUCT_PREFIXES = [
  'src/modules/',
  'src/runner/',
  'src/settings-ui/',
];

export const PRODUCT_PATH_LABEL_MAP = [
  ['src/modules/MouseUtils/MousePointerCrosshairs/', 'Product-Mouse Pointer Crosshairs'],
  ['src/modules/MouseUtils/MouseHighlighter/', 'Product-Mouse Highlighter'],
  ['src/modules/MouseUtils/FindMyMouse/', 'Product-Find My Mouse'],
  ['src/modules/MouseUtils/CursorWrap/', 'Product-Cursor Wrap'],
  ['src/modules/MouseUtils/MouseJump', 'Product-Mouse Jump'],
  ['src/modules/AltWindowCycle/', 'Product-Window Hopper'],
  ['src/modules/MouseUtils/', 'Product-Mouse Utilities'],
  ['src/modules/AdvancedPaste/', 'Product-Advanced Paste'],
  ['src/modules/alwaysontop/', 'Product-Always On Top'],
  ['src/modules/awake/', 'Product-Awake'],
  ['src/modules/cmdNotFound/', 'Product-CommandNotFound'],
  ['src/modules/cmdpal/', 'Product-Command Palette'],
  ['src/modules/colorPicker/', 'Product-Color Picker'],
  ['src/modules/CropAndLock/', 'Product-CropAndLock'],
  ['src/modules/EnvironmentVariables/', 'Product-Environment Variables'],
  ['src/modules/fancyzones/', 'Product-FancyZones'],
  ['src/modules/FileLocksmith/', 'Product-File Locksmith'],
  ['src/modules/GrabAndMove/', 'Product-Grab And Move'],
  ['src/modules/Hosts/', 'Product-Hosts File Editor'],
  ['src/modules/imageresizer/', 'Product-Image Resizer'],
  ['src/modules/interface/', 'Product-General'],
  ['src/modules/keyboardmanager/', 'Product-Keyboard Manager'],
  ['src/modules/launcher/', 'Product-PowerToys Run'],
  ['src/modules/LightSwitch/', 'Product-LightSwitch'],
  ['src/modules/MeasureTool/', 'Product-Screen Ruler'],
  ['src/modules/MouseWithoutBorders/', 'Product-Mouse Without Borders'],
  ['src/modules/NewPlus/', 'Product-New+'],
  ['src/modules/peek/', 'Product-Peek'],
  ['src/modules/poweraccent/', 'Product-Quick Accent'],
  ['src/modules/powerdisplay/', 'Product-PowerDisplay'],
  ['src/modules/PowerOCR/', 'Product-Text Extractor'],
  ['src/modules/powerrename/', 'Product-PowerRename'],
  ['src/modules/previewpane/', 'Product-File Explorer'],
  ['src/modules/registrypreview/', 'Product-Registry Preview'],
  ['src/modules/ShortcutGuide/', 'Product-Shortcut Guide'],
  ['src/modules/shortcut_guide/', 'Product-Shortcut Guide'],
  ['src/modules/Workspaces/', 'Product-Workspaces'],
  ['src/modules/ZoomIt/', 'Product-ZoomIt'],
  ['src/runner/', 'Product-General'],
  ['src/common/', 'Product-General'],
  ['src/settings-ui/', 'Product-Settings'],
];

const SORTED_PRODUCT_PATH_LABEL_MAP = [...PRODUCT_PATH_LABEL_MAP]
  .sort((left, right) => right[0].length - left[0].length);

export class ApiError extends Error {
  constructor(message, status, details = '') {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.details = details;
  }
}

function boundedString(value, maxLength, fallback = '') {
  if (typeof value !== 'string') {
    return fallback;
  }
  const normalized = value.replace(/\u0000/g, '').trim();
  return normalized.slice(0, maxLength);
}

function markdownPlainText(value, maxLength, fallback = '') {
  return boundedString(value, maxLength, fallback)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('@', '@\u200b')
    .replace(/[\r\n]+/g, ' ')
    .replace(/([\\`*_[\]])/g, '\\$1');
}

function formatInlineCode(value) {
  return `\`${boundedString(String(value).replaceAll('`', '').replace(/\r?\n+/g, ' '), 500)}\``;
}

export function deriveVisualAssessment(requiresVisualEvidence) {
  return requiresVisualEvidence
    ? {
      visualEvidenceRequirement: 'REQUIRED',
      visualEvidenceReason:
        'The pull request changes product UI files, so reviewers need to see the visible result.',
    }
    : {
      visualEvidenceRequirement: 'NOT_NEEDED',
      visualEvidenceReason:
        'The changed files do not indicate a visible UI change.',
    };
}

function sleep(milliseconds) {
  return new Promise((resolve) => {
    setTimeout(resolve, Math.max(0, milliseconds));
  });
}

async function mapWithConcurrency(items, limit, mapper) {
  const list = Array.isArray(items) ? items : [];
  const results = new Array(list.length);
  const boundedLimit = Math.max(1, Math.min(limit, list.length || 1));
  let cursor = 0;
  async function worker() {
    while (cursor < list.length) {
      const index = cursor;
      cursor += 1;
      results[index] = await mapper(list[index], index);
    }
  }
  await Promise.all(
    Array.from({ length: boundedLimit }, () => worker()),
  );
  return results;
}

function uniqueSorted(values) {
  return [...new Set(values.filter(Boolean))]
    .sort((left, right) => left.localeCompare(right));
}

export function normalizePath(value) {
  if (typeof value !== 'string') {
    return '';
  }
  return value
    .replace(/\\/g, '/')
    .replace(/^\.\//, '')
    .replace(/^\/+/, '')
    .trim();
}

export function deriveProductLabelsFromPaths(paths, currentLabels = []) {
  if (!Array.isArray(paths)) {
    throw new Error('Changed paths must be an array');
  }

  const labels = new Set();
  for (const rawPath of paths) {
    const changedPath = normalizePath(rawPath).toLowerCase();
    if (!changedPath) {
      continue;
    }
    const match = SORTED_PRODUCT_PATH_LABEL_MAP.find(
      ([prefix]) => changedPath.startsWith(prefix.toLowerCase()),
    );
    if (match) {
      labels.add(match[1]);
    }
  }

  const existingProductLabels = (Array.isArray(currentLabels) ? currentLabels : [])
    .map((entry) => typeof entry === 'string' ? entry : entry?.name)
    .filter((label) => typeof label === 'string' && label.startsWith('Product-'));
  const matchedProductLabels = [...existingProductLabels, ...labels];
  if (matchedProductLabels.some((label) => label !== 'Product-Settings')) {
    labels.delete('Product-Settings');
  }
  if (matchedProductLabels.some(
    (label) => !['Product-General', 'Product-Settings'].includes(label),
  )) {
    labels.delete('Product-General');
  }

  return uniqueSorted([...labels]);
}

function isTestPath(changedPath) {
  return /(^|\/)(test|tests|unittests|uitests)(\/|$)/i.test(changedPath)
    || /\.(?:spec|test)\.[^.]+$/i.test(changedPath);
}

function classifyPath(changedPath) {
  const extension = path.extname(changedPath).toLowerCase();
  if (
    changedPath.endsWith('.md')
    || changedPath.startsWith('doc/')
    || changedPath.startsWith('docs/')
  ) {
    return 'docs';
  }
  if (isTestPath(changedPath)) {
    return 'tests';
  }
  if (
    changedPath.startsWith('.github/')
    || changedPath.startsWith('.pipelines/')
    || changedPath.startsWith('tools/')
    || changedPath.startsWith('installer/')
  ) {
    return 'infrastructure';
  }
  if (
    VISUAL_FILE_EXTENSIONS.has(extension)
    && VISUAL_PRODUCT_PREFIXES.some((prefix) => changedPath.startsWith(prefix))
  ) {
    return 'product-ui';
  }
  return 'product-code';
}

export function classifyChangedPaths(paths) {
  if (!Array.isArray(paths)) {
    throw new Error('Changed paths must be an array');
  }
  const normalizedPaths = uniqueSorted(
    paths.map((entry) => normalizePath(entry)).filter(Boolean),
  );
  const pathCategories = normalizedPaths.map((changedPath) => ({
    changedPath,
    category: classifyPath(changedPath),
  }));
  const categories = uniqueSorted(pathCategories.map((entry) => entry.category));
  const visualCandidatePaths = pathCategories
    .filter((entry) => entry.category === 'product-ui')
    .map((entry) => entry.changedPath);

  return {
    changedPathCount: normalizedPaths.length,
    normalizedPaths,
    categories,
    visualCandidatePaths,
    requiresVisualEvidence: visualCandidatePaths.length > 0,
  };
}

export function findClosingIssueReferences(body) {
  const text = typeof body === 'string' ? body : '';
  const matches = [];
  const seen = new Set();
  const regex =
    /\b(closes?|closed|fixes?|fixed|resolves?|resolved)\s*:?\s+((?:[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+)\s*)?#(\d+)\b/gi;

  for (const match of text.matchAll(regex)) {
    const repositoryFullName = boundedString(match[2], 200).replace(/\s+/g, '');
    const issueNumber = Number(match[3]);
    const seenKey = `${repositoryFullName.toLowerCase()}#${issueNumber}`;
    if (!Number.isSafeInteger(issueNumber) || issueNumber <= 0 || seen.has(seenKey)) {
      continue;
    }
    seen.add(seenKey);
    matches.push({
      keyword: match[1].toLowerCase(),
      issueNumber,
      repositoryFullName: repositoryFullName || null,
    });
    if (matches.length >= MAX_CLOSING_REFERENCES) {
      break;
    }
  }

  return matches;
}

function formatClosingReference(reference) {
  return reference.repositoryFullName
    ? `${reference.repositoryFullName}#${reference.issueNumber}`
    : `#${reference.issueNumber}`;
}

function invalidClosingReasonLabel(reason) {
  switch (reason) {
    case 'different-repository':
      return 'different repo';
    case 'pull-request':
      return 'pull request';
    case 'not-found':
      return 'not found';
    default:
      return 'invalid';
  }
}

function formatInvalidClosingReference(reference) {
  return `${formatInlineCode(formatClosingReference(reference))} (${invalidClosingReasonLabel(reference.reason)})`;
}

export async function verifyClosingIssueReferences({
  api,
  repositoryFullName,
  references,
}) {
  const normalizedRepositoryFullName = boundedString(repositoryFullName, 200).toLowerCase();
  if (!normalizedRepositoryFullName) {
    throw new Error('Repository full name is required for closing issue verification');
  }

  const boundedReferences = (Array.isArray(references) ? references : [])
    .slice(0, MAX_CLOSING_REFERENCES);
  const results = await mapWithConcurrency(
    boundedReferences,
    CLOSING_VERIFY_CONCURRENCY,
    async (reference) => {
      if (reference.repositoryFullName
        && reference.repositoryFullName.toLowerCase() !== normalizedRepositoryFullName) {
        return {
          ...reference,
          reason: 'different-repository',
        };
      }

      try {
        const issue = await api.getIssue(reference.issueNumber);
        if (issue?.pull_request) {
          return {
            ...reference,
            reason: 'pull-request',
          };
        }
        return {
          ...reference,
          title: boundedString(issue?.title, 300, `Issue ${reference.issueNumber}`),
        };
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) {
          return {
            ...reference,
            reason: 'not-found',
          };
        }
        throw error;
      }
    },
  );

  return {
    validReferences: results.filter((reference) => !reference.reason),
    invalidReferences: results.filter((reference) => Boolean(reference.reason)),
  };
}

function isVisualMediaUrl(value) {
  const url = typeof value === 'string' ? value.trim() : '';
  if (!url) {
    return false;
  }
  if (/^https:\/\/github\.com\/user-attachments\/assets\/[^\s<>)"]+/i.test(url)) {
    return true;
  }
  return /\.(?:png|jpe?g|gif|webp|bmp|svg|mp4|mov|webm|m4v)(?:[?#].*)?$/i.test(url);
}

export function findVisualEvidence(body) {
  const text = typeof body === 'string' ? body : '';
  const evidenceTypes = [];

  const patterns = [
    {
      type: 'GitHub user-attachment URL',
      regex: /https:\/\/github\.com\/user-attachments\/(?:assets\/[^\s<>)"]+|files\/[^\s<>)"]+\.(?:png|jpe?g|gif|webp|bmp|svg|mp4|mov|webm|m4v))(?:[?#][^\s<>)"]*)?/gi,
    },
    {
      type: 'Recognized video link',
      regex: /https?:\/\/(?:www\.)?(?:youtu\.be\/|youtube\.com\/watch\?[^\s<>"']*v=|vimeo\.com\/|loom\.com\/share\/|streamable\.com\/)[^\s<>)"]+/gi,
    },
  ];

  for (const { type, regex } of patterns) {
    if (regex.test(text)) {
      evidenceTypes.push(type);
    }
  }

  for (const match of text.matchAll(
    /!\[[^\]]*]\(\s*(?:<([^>]+)>|([^)\s]+))(?:\s+"[^"]*")?\s*\)/gi,
  )) {
    if (isVisualMediaUrl(match[1] || match[2])) {
      evidenceTypes.push('Markdown image');
      break;
    }
  }

  for (const match of text.matchAll(
    /<(img|video|source)\b[^>]*\bsrc\s*=\s*['"]([^'"]+)['"][^>]*>/gi,
  )) {
    if (!isVisualMediaUrl(match[2])) {
      continue;
    }
    evidenceTypes.push(match[1].toLowerCase() === 'img'
      ? 'HTML image tag'
      : 'HTML video tag');
  }

  return {
    found: evidenceTypes.length > 0,
    types: uniqueSorted(evidenceTypes),
  };
}

export function isMergeabilityKnown(pullRequest) {
  if (pullRequest?.mergeable === true || pullRequest?.mergeable === false) {
    return true;
  }
  const mergeableState = boundedString(
    pullRequest?.mergeable_state ?? pullRequest?.mergeStateStatus,
    40,
  ).toLowerCase();
  return mergeableState !== '' && mergeableState !== 'unknown';
}

export function hasMergeConflict(pullRequest) {
  const mergeableState = boundedString(
    pullRequest?.mergeable_state ?? pullRequest?.mergeStateStatus,
    40,
  ).toLowerCase();
  return pullRequest?.mergeable === false
    || mergeableState === 'dirty'
    || mergeableState === 'conflicting';
}

// GitHub returns `mergeable: null` / `mergeable_state: unknown` while it is still
// computing mergeability. Re-fetch until the state is known so a conflicting PR
// is never treated as ready by default.
export async function getPullRequestWithMergeability(
  api,
  pullNumber,
  {
    maxAttempts = MERGEABILITY_MAX_ATTEMPTS,
    delayMs = MERGEABILITY_RETRY_DELAY_MS,
    sleepImpl = sleep,
  } = {},
) {
  let pullRequest = await api.getPullRequest(pullNumber);
  let attempt = 1;
  while (!isMergeabilityKnown(pullRequest) && attempt < maxAttempts) {
    await sleepImpl(delayMs);
    pullRequest = await api.getPullRequest(pullNumber);
    attempt += 1;
  }
  return {
    pullRequest,
    mergeabilityKnown: isMergeabilityKnown(pullRequest),
  };
}

function buildContributingUrl(repositoryHtmlUrl, baseRef) {
  const repoUrl = boundedString(repositoryHtmlUrl, 500);
  const branch = boundedString(baseRef, 200, 'main');
  if (!repoUrl) {
    throw new Error('Repository HTML URL is required to build CONTRIBUTING.md links');
  }
  return `${repoUrl}/blob/${branch}/CONTRIBUTING.md`;
}

export function buildIntakeReport({
  changedPaths,
  body,
  repositoryHtmlUrl,
  baseRef,
  authorLogin,
  isDraft = false,
  mergeConflict = false,
  mergeabilityKnown = true,
  verifiedClosingIssues,
  invalidClosingIssues,
}) {
  const ownership = classifyChangedPaths(changedPaths);
  const visualAssessment = deriveVisualAssessment(ownership.requiresVisualEvidence);
  const closingIssues = Array.isArray(verifiedClosingIssues)
    ? verifiedClosingIssues
    : findClosingIssueReferences(body);
  const invalidClosingReferences = Array.isArray(invalidClosingIssues)
    ? invalidClosingIssues
    : [];
  const visualEvidence = findVisualEvidence(body);
  const authorActions = [];
  const recommendations = [];

  if (mergeConflict) {
    authorActions.push(
      'Resolve the merge conflicts with the target branch.',
    );
  }
  if (!closingIssues.length && !invalidClosingReferences.length) {
    recommendations.push(
      'Link the issue this PR fixes using a closing keyword such as `Closes #123`.',
    );
  }
  if (invalidClosingReferences.length) {
    authorActions.push(
      `Replace the invalid closing reference${invalidClosingReferences.length === 1 ? '' : 's'} ${
        invalidClosingReferences.map((reference) => formatInvalidClosingReference(reference)).join(', ')
      } with a valid issue, for example \`Closes #123\`.`,
    );
  }
  if (
    visualAssessment.visualEvidenceRequirement === 'REQUIRED'
    && !visualEvidence.found
  ) {
    authorActions.push(
      'Add a screenshot, GIF, or video to the PR description so reviewers can validate the visible change.',
    );
  }
  const needsAuthorFeedback = authorActions.length > 0;
  if (isDraft) {
    authorActions.push('Mark the pull request as ready for review.');
  }

  return {
    ...ownership,
    closingIssues,
    invalidClosingIssues: invalidClosingReferences,
    visualEvidence,
    visualAssessment,
    authorActions,
    recommendations,
    authorLogin: boundedString(authorLogin, 100),
    mergeConflict,
    mergeabilityKnown,
    needsAuthorFeedback,
    readyForReview: authorActions.length === 0 && mergeabilityKnown,
    contributingUrl: buildContributingUrl(repositoryHtmlUrl, baseRef),
  };
}

export function parseFeedbackSince(body) {
  const match = typeof body === 'string'
    ? body.match(/<!-- powertoys-pr-intake:feedback-since:([0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:.]+Z) -->/)
    : null;
  if (!match) {
    return null;
  }
  const timestamp = new Date(match[1]);
  return Number.isNaN(timestamp.getTime()) ? null : timestamp.toISOString();
}

export function determineFeedbackSince({
  needsAuthorFeedback,
  existingCommentBody,
  action,
  senderLogin,
  authorLogin,
  now = new Date(),
}) {
  if (!needsAuthorFeedback) {
    return null;
  }
  const currentTimestamp = now instanceof Date ? now : new Date(now);
  if (Number.isNaN(currentTimestamp.getTime())) {
    throw new Error('The feedback timestamp must be a valid date');
  }
  const existing = parseFeedbackSince(existingCommentBody);
  const normalizedSender = boundedString(senderLogin, 100).toLowerCase();
  const normalizedAuthor = boundedString(authorLogin, 100).toLowerCase();
  const isAuthorActivity = normalizedAuthor
    && normalizedSender === normalizedAuthor
    && action !== 'opened';
  return !existing || isAuthorActivity
    ? currentTimestamp.toISOString()
    : existing;
}

export function renderIntakeComment(report, feedbackSince = null) {
  const authorMention = /^[A-Za-z0-9-]+$/.test(report.authorLogin)
    ? `@${report.authorLogin}, `
    : '';
  const feedbackMarker = report.needsAuthorFeedback && feedbackSince
    ? `\n<!-- ${FEEDBACK_SINCE_MARKER}:${feedbackSince} -->`
    : '';
  const deadlineNotice = report.needsAuthorFeedback
    ? `\nIf there is no author response within ${STALE_FEEDBACK_DAYS} days, this PR will be automatically closed.\n`
    : '';
  const requirement = report.visualAssessment.visualEvidenceRequirement;
  const visualEvidenceState = report.visualEvidence.found
    ? 'Visual evidence was detected in the PR description.'
    : requirement === 'REQUIRED'
      ? 'Visual evidence is currently missing.'
      : 'No visual evidence is expected.';
  const visualReason = markdownPlainText(
    report.visualAssessment.visualEvidenceReason,
    500,
    'No explanation was provided.',
  );
  const requirementLabel = requirement === 'REQUIRED' ? 'Required' : 'Not needed';
  const statusSection = report.readyForReview
    ? `## ✅ Ready for review

This PR passed the automated intake checks and is ready for maintainer review.
`
    : `## Author action

${authorMention}please update the following before review:

${report.authorActions.map((entry) => `- ${entry}`).join('\n')}

See the [contribution guide](${report.contributingUrl}) for the full checklist.
${deadlineNotice}`;
  const recommendationSection = report.recommendations.length
    ? `## Recommendation

${report.recommendations.map((entry) => `> ${entry}`).join('\n')}

`
    : '';

  return `${CANONICAL_MARKER}
## 🧭 PR intake${feedbackMarker}

**Visual evidence:** ${requirementLabel} — ${visualReason} ${visualEvidenceState}

${recommendationSection}
${statusSection}
_Automated PR intake; PowerToys maintainers make final decisions._
`;
}

export function renderAllClearComment() {
  return `${CANONICAL_MARKER}
## ✅ PR intake

All automated intake checks now pass. Thanks for the updates!

_Automated PR intake; PowerToys maintainers make final decisions._
`;
}

export function selectCanonicalComment(
  comments,
  expectedLogin = ACTIONS_BOT_LOGIN,
  expectedId = ACTIONS_BOT_ID,
) {
  if (!Array.isArray(comments)) {
    throw new Error('Comments must be an array');
  }
  const trusted = comments.filter((comment) =>
    Number.isSafeInteger(comment?.id)
    && comment.user?.login === expectedLogin
    && comment.user?.type === 'Bot'
    && (expectedId === null || comment.user?.id === expectedId)
    && typeof comment.body === 'string'
    && comment.body.includes(CANONICAL_MARKER));
  trusted.sort((left, right) => left.id - right.id);
  return {
    canonical: trusted[0] ?? null,
    extras: trusted.slice(1),
  };
}

async function listAllComments(api, issueNumber) {
  const comments = [];
  for (let page = 1; page <= MAX_COMMENT_PAGES; page += 1) {
    const batch = await api.listIssueComments(issueNumber, page, PAGE_SIZE);
    if (!Array.isArray(batch)) {
      throw new Error('GitHub comments response must be an array');
    }
    comments.push(...batch);
    if (batch.length < PAGE_SIZE) {
      return comments;
    }
  }
  throw new Error(`Comment scan exceeded ${MAX_COMMENT_PAGES * PAGE_SIZE} items`);
}

export async function upsertCanonicalComment({
  api,
  issueNumber,
  body,
  comments = null,
}) {
  const existingComments = Array.isArray(comments)
    ? comments
    : await listAllComments(api, issueNumber);
  const { canonical, extras } = selectCanonicalComment(existingComments);

  let savedComment;
  let operation;
  if (canonical) {
    savedComment = await api.updateIssueComment(canonical.id, body);
    operation = 'updated';
  } else {
    savedComment = await api.createIssueComment(issueNumber, body);
    operation = 'created';
  }

  const deletedExtraComments = [];
  for (const extra of extras) {
    await api.deleteIssueComment(extra.id);
    deletedExtraComments.push(extra.id);
  }

  return {
    comment: savedComment,
    operation,
    deletedExtraComments,
  };
}

export async function deleteCanonicalComments({
  api,
  issueNumber,
  comments = null,
}) {
  const existingComments = Array.isArray(comments)
    ? comments
    : await listAllComments(api, issueNumber);
  const { canonical, extras } = selectCanonicalComment(existingComments);
  const trustedComments = canonical ? [canonical, ...extras] : [];
  const deletedCommentIds = [];
  for (const comment of trustedComments) {
    await api.deleteIssueComment(comment.id);
    deletedCommentIds.push(comment.id);
  }
  return deletedCommentIds;
}

export function planManagedLabelChanges(
  currentLabels,
  desiredLabels,
  managedLabels = [],
) {
  const current = new Set(uniqueSorted(
    (Array.isArray(currentLabels) ? currentLabels : [])
      .map((entry) => typeof entry === 'string' ? entry : entry?.name)
      .map((entry) => boundedString(entry, 100))
      .filter(Boolean),
  ));
  const desired = new Set(uniqueSorted(
    (Array.isArray(desiredLabels) ? desiredLabels : [])
      .map((entry) => boundedString(entry, 100))
      .filter(Boolean),
  ));
  const managed = new Set(uniqueSorted(
    (Array.isArray(managedLabels) ? managedLabels : [])
      .map((entry) => boundedString(entry, 100))
      .filter(Boolean),
  ));

  return {
    add: [...desired].filter((entry) => !current.has(entry)),
    remove: [...current].filter((entry) => managed.has(entry) && !desired.has(entry)),
  };
}

async function syncLabelChanges(api, issueNumber, labelPlan) {
  for (const label of labelPlan.remove) {
    await api.removeLabel(issueNumber, label);
  }
  if (labelPlan.add.length) {
    await api.addLabels(issueNumber, labelPlan.add);
  }
}

async function listAllPullRequestFileDetails(api, pullNumber) {
  const files = [];
  for (let page = 1; page <= MAX_FILE_PAGES; page += 1) {
    const batch = await api.listPullRequestFiles(pullNumber, page, PAGE_SIZE);
    if (!Array.isArray(batch)) {
      throw new Error('GitHub pull request files response must be an array');
    }
    files.push(...batch);
    if (batch.length < PAGE_SIZE) {
      return files;
    }
  }
  throw new Error(`Pull request file scan exceeded ${MAX_FILE_PAGES * PAGE_SIZE} items`);
}

function changedPathsFromFileDetails(files) {
  return (Array.isArray(files) ? files : []).flatMap((file) => [
    boundedString(file?.filename, 500),
    boundedString(file?.previous_filename, 500),
  ]).filter(Boolean);
}

function parseIssueLabels(issue) {
  return Array.isArray(issue?.labels)
    ? issue.labels
      .map((entry) => typeof entry === 'string' ? entry : entry?.name)
      .filter((entry) => typeof entry === 'string' && entry.trim().length > 0)
    : [];
}

export async function runPullRequestIntake({ api, event }) {
  if (!event?.repository) {
    throw new Error('The GitHub event payload must contain repository data');
  }

  const pullNumber = Number(
    event.pull_request?.number
    ?? (event.issue?.pull_request ? event.issue.number : null),
  );
  if (!Number.isSafeInteger(pullNumber) || pullNumber <= 0) {
    throw new Error('The GitHub event payload must identify a pull request');
  }
  const { pullRequest, mergeabilityKnown } = await getPullRequestWithMergeability(
    api,
    pullNumber,
  );
  const issueNumber = pullNumber;

  const issue = await api.getIssue(issueNumber);
  const currentLabels = parseIssueLabels(issue);
  const fileDetails = await listAllPullRequestFileDetails(api, issueNumber);
  const changedPaths = changedPathsFromFileDetails(fileDetails);
  const pathProductLabels = deriveProductLabelsFromPaths(changedPaths, currentLabels);
  if (pullRequest.draft === true) {
    const labelPlan = planManagedLabelChanges(
      currentLabels,
      pathProductLabels,
      [
        READY_FOR_REVIEW_LABEL,
        NEEDS_AUTHOR_FEEDBACK_LABEL,
        LEGACY_READY_FOR_REVIEW_LABEL,
      ],
    );
    await syncLabelChanges(api, issueNumber, labelPlan);
    const deletedCanonicalCommentIds = await deleteCanonicalComments({
      api,
      issueNumber,
    });
    return {
      issueNumber,
      skippedDraft: true,
      changedPathCount: changedPaths.length,
      pathProductLabels,
      labelPlan,
      deletedCanonicalCommentIds,
    };
  }

  const closingReferenceVerification = await verifyClosingIssueReferences({
    api,
    repositoryFullName: event.repository.full_name,
    references: findClosingIssueReferences(pullRequest.body ?? ''),
  });
  const report = buildIntakeReport({
    changedPaths,
    body: pullRequest.body ?? '',
    repositoryHtmlUrl: event.repository.html_url,
    baseRef: pullRequest.base?.ref ?? 'main',
    authorLogin: pullRequest.user?.login ?? '',
    isDraft: pullRequest.draft === true,
    mergeConflict: hasMergeConflict(pullRequest),
    mergeabilityKnown,
    verifiedClosingIssues: closingReferenceVerification.validReferences,
    invalidClosingIssues: closingReferenceVerification.invalidReferences,
  });
  const comments = await listAllComments(api, issueNumber);
  const { canonical } = selectCanonicalComment(comments);
  const feedbackSince = determineFeedbackSince({
    needsAuthorFeedback: report.needsAuthorFeedback,
    existingCommentBody: canonical?.body ?? '',
    action: event.action,
    senderLogin: event.sender?.login,
    authorLogin: pullRequest.user?.login,
  });
  const desiredLabels = [
    ...(report.readyForReview ? [READY_FOR_REVIEW_LABEL] : []),
    ...(report.needsAuthorFeedback ? [NEEDS_AUTHOR_FEEDBACK_LABEL] : []),
    ...pathProductLabels,
  ];
  const labelPlan = planManagedLabelChanges(
    currentLabels,
    desiredLabels,
    [
      READY_FOR_REVIEW_LABEL,
      NEEDS_AUTHOR_FEEDBACK_LABEL,
      LEGACY_READY_FOR_REVIEW_LABEL,
    ],
  );

  await syncLabelChanges(api, issueNumber, labelPlan);

  // Only surface a comment when there is something for the author to act on or
  // consider. When a previously flagged PR becomes clean we replace the stale
  // comment with a short all-clear note; when nothing was ever posted we stay
  // silent to avoid noise on already-healthy PRs.
  const hasRemarks = report.authorActions.length > 0
    || report.recommendations.length > 0;
  let commentResult = {
    operation: 'skipped',
    comment: null,
    deletedExtraComments: [],
  };
  if (hasRemarks) {
    commentResult = await upsertCanonicalComment({
      api,
      issueNumber,
      body: renderIntakeComment(report, feedbackSince),
      comments,
    });
  } else if (canonical) {
    commentResult = await upsertCanonicalComment({
      api,
      issueNumber,
      body: renderAllClearComment(),
      comments,
    });
  }

  return {
    issueNumber,
    changedPathCount: changedPaths.length,
    pathProductLabels,
    labelPlan,
    commentResult: {
      operation: commentResult.operation,
      commentId: commentResult.comment?.id ?? null,
      deletedExtraComments: commentResult.deletedExtraComments,
    },
    requiresVisualEvidence: report.requiresVisualEvidence,
    visualEvidenceRequirement: report.visualAssessment.visualEvidenceRequirement,
    visualEvidenceFound: report.visualEvidence.found,
    mergeConflict: report.mergeConflict,
    mergeabilityKnown: report.mergeabilityKnown,
    closingIssueCount: report.closingIssues.length,
    invalidClosingIssueCount: report.invalidClosingIssues.length,
    needsAuthorFeedback: report.needsAuthorFeedback,
    feedbackSince,
    readyForReview: report.readyForReview,
  };
}

export class GitHubApi {
  constructor({
    token,
    owner,
    repo,
    apiBaseUrl = process.env.GITHUB_API_URL ?? 'https://api.github.com',
    fetchImpl = fetch,
  }) {
    if (!token) {
      throw new Error('GITHUB_TOKEN is required');
    }
    if (!owner || !repo) {
      throw new Error('Repository owner and name are required');
    }
    this.token = token;
    this.owner = owner;
    this.repo = repo;
    this.apiBaseUrl = apiBaseUrl.replace(/\/+$/, '');
    this.fetchImpl = fetchImpl;
  }

  async request(method, route, body = undefined) {
    const response = await this.fetchImpl(
      `${this.apiBaseUrl}/repos/${this.owner}/${this.repo}${route}`,
      {
        method,
        headers: {
          Accept: 'application/vnd.github+json',
          Authorization: `Bearer ${this.token}`,
          'Content-Type': 'application/json',
          'User-Agent': 'powertoys-pr-intake',
        },
        body: body === undefined ? undefined : JSON.stringify(body),
      },
    );

    const raw = await response.text();
    const payload = raw ? JSON.parse(raw) : null;
    if (!response.ok) {
      throw new ApiError(
        boundedString(payload?.message, 300, 'GitHub API request failed'),
        response.status,
        raw.slice(0, 500),
      );
    }
    return payload;
  }

  listPullRequestFiles(pullNumber, page, perPage) {
    return this.request(
      'GET',
      `/pulls/${pullNumber}/files?page=${page}&per_page=${perPage}`,
    );
  }

  listIssueComments(issueNumber, page, perPage) {
    return this.request(
      'GET',
      `/issues/${issueNumber}/comments?page=${page}&per_page=${perPage}`,
    );
  }

  createIssueComment(issueNumber, body) {
    return this.request('POST', `/issues/${issueNumber}/comments`, { body });
  }

  updateIssueComment(commentId, body) {
    return this.request('PATCH', `/issues/comments/${commentId}`, { body });
  }

  deleteIssueComment(commentId) {
    return this.request('DELETE', `/issues/comments/${commentId}`);
  }

  getIssue(issueNumber) {
    return this.request('GET', `/issues/${issueNumber}`);
  }

  getPullRequest(pullNumber) {
    return this.request('GET', `/pulls/${pullNumber}`);
  }

  addLabels(issueNumber, labels) {
    return this.request('POST', `/issues/${issueNumber}/labels`, { labels });
  }

  removeLabel(issueNumber, label) {
    return this.request(
      'DELETE',
      `/issues/${issueNumber}/labels/${encodeURIComponent(label)}`,
    );
  }
}

async function main() {
  const eventPath = process.argv[2] || process.env.GITHUB_EVENT_PATH;
  if (!eventPath) {
    throw new Error('The GitHub event payload path is required');
  }

  const event = JSON.parse(await fs.readFile(eventPath, 'utf8'));
  const repository = boundedString(
    event?.repository?.full_name ?? process.env.GITHUB_REPOSITORY,
    200,
  );
  const [owner, repo] = repository.split('/');
  if (!owner || !repo) {
    throw new Error('Unable to determine the repository owner and name');
  }

  const api = new GitHubApi({
    token: process.env.GITHUB_TOKEN,
    owner,
    repo,
  });

  const result = await runPullRequestIntake({ api, event });
  console.log(JSON.stringify(result, null, 2));
}

const ENTRYPOINT = fileURLToPath(import.meta.url);
if (process.argv[1] && path.resolve(process.argv[1]) === ENTRYPOINT) {
  main().catch((error) => {
    console.error(error instanceof Error ? error.stack : String(error));
    process.exitCode = 1;
  });
}
