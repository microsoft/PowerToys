import crypto from 'node:crypto';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const CANONICAL_MARKER = '<!-- powertoys-pr-intake:canonical:v1 -->';
export const ACTIONS_BOT_LOGIN = 'github-actions[bot]';
export const ACTIONS_BOT_ID = 41898282;
export const READY_FOR_REVIEW_LABEL = 'Needs-Review';
export const NEEDS_AUTHOR_FEEDBACK_LABEL = 'Needs-Author-Feedback';
export const FEEDBACK_SINCE_MARKER = 'powertoys-pr-intake:feedback-since';
export const STALE_FEEDBACK_DAYS = 7;

const PAGE_SIZE = 100;
const MAX_COMMENT_PAGES = 10;
const MAX_FILE_PAGES = 10;
const MAX_AI_BODY_LENGTH = 12000;
const MAX_AI_FILE_COUNT = 100;
const MAX_AI_PATCH_FILES = 12;
const MAX_AI_PATCH_LENGTH = 2400;

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

export function normalizeAiAssessment(value, fallbackRequiresVisualEvidence = false) {
  const requestedRequirement = boundedString(
    value?.visualEvidenceRequirement ?? value?.visual_evidence_requirement,
    40,
  ).toUpperCase();
  const visualEvidenceRequirement = [
    'REQUIRED',
    'RECOMMENDED',
    'NOT_NEEDED',
  ].includes(requestedRequirement)
    ? requestedRequirement
    : (fallbackRequiresVisualEvidence ? 'REQUIRED' : 'NOT_NEEDED');
  const defaultReason = fallbackRequiresVisualEvidence
    ? 'The changed paths indicate a product UI change.'
    : 'The deterministic fallback did not identify a visual UI change.';

  return {
    inputSha256: boundedString(
      value?.inputSha256 ?? value?.input_sha256,
      64,
    ).toLowerCase(),
    summary: boundedString(
      value?.summary,
      800,
      'Automated summary unavailable.',
    ),
    visualEvidenceRequirement,
    visualEvidenceReason: boundedString(
      value?.visualEvidenceReason ?? value?.visual_evidence_reason,
      500,
      defaultReason,
    ),
  };
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

  const results = await Promise.all((Array.isArray(references) ? references : []).map(async (reference) => {
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
  }));

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

export function hasMergeConflict(pullRequest) {
  const mergeableState = boundedString(
    pullRequest?.mergeable_state ?? pullRequest?.mergeStateStatus,
    40,
  ).toLowerCase();
  return pullRequest?.mergeable === false
    || mergeableState === 'dirty'
    || mergeableState === 'conflicting';
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
  verifiedClosingIssues,
  invalidClosingIssues,
  aiAssessment,
}) {
  const ownership = classifyChangedPaths(changedPaths);
  const normalizedAiAssessment = normalizeAiAssessment(
    aiAssessment,
    ownership.requiresVisualEvidence,
  );
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
    normalizedAiAssessment.visualEvidenceRequirement === 'REQUIRED'
    && !visualEvidence.found
  ) {
    authorActions.push(
      'Add a screenshot, GIF, or video to the PR description because this change affects product UI.',
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
    aiAssessment: normalizedAiAssessment,
    authorActions,
    recommendations,
    authorLogin: boundedString(authorLogin, 100),
    mergeConflict,
    needsAuthorFeedback,
    readyForReview: authorActions.length === 0,
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
  const requirementLabels = {
    REQUIRED: 'Required',
    RECOMMENDED: 'Recommended',
    NOT_NEEDED: 'Not needed',
  };
  const requirement = report.aiAssessment.visualEvidenceRequirement;
  const visualEvidenceState = report.visualEvidence.found
    ? 'Visual evidence was detected in the PR description.'
    : requirement === 'REQUIRED'
      ? 'Visual evidence is currently missing.'
      : requirement === 'RECOMMENDED'
        ? 'Visual evidence is not present, but it would help reviewers.'
        : 'No visual evidence is expected.';
  const summary = markdownPlainText(
    report.aiAssessment.summary,
    800,
    'Automated summary unavailable.',
  );
  const visualReason = markdownPlainText(
    report.aiAssessment.visualEvidenceReason,
    500,
    'No explanation was provided.',
  );
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

**Summary:** ${summary}

**Visual evidence:** ${requirementLabels[requirement]} — ${visualReason} ${visualEvidenceState}

${recommendationSection}
${statusSection}
_AI-assisted automated intake; PowerToys maintainers make final decisions._
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

async function syncManagedLabels(api, issueNumber, labelPlan) {
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

export function buildPullRequestAiContext({ pullRequest, files }) {
  const fileDetails = Array.isArray(files) ? files : [];
  const changedPaths = changedPathsFromFileDetails(fileDetails);
  const ownership = classifyChangedPaths(changedPaths);
  const visualEvidence = findVisualEvidence(pullRequest?.body ?? '');
  const source = {
    number: Number(pullRequest?.number),
    title: boundedString(pullRequest?.title, 500),
    body: boundedString(pullRequest?.body, MAX_AI_BODY_LENGTH),
    draft: pullRequest?.draft === true,
    baseRef: boundedString(pullRequest?.base?.ref, 200),
    headSha: boundedString(pullRequest?.head?.sha, 100),
    changedFiles: fileDetails.slice(0, MAX_AI_FILE_COUNT).map((file) => ({
      filename: boundedString(file?.filename, 500),
      previousFilename: boundedString(file?.previous_filename, 500),
      status: boundedString(file?.status, 40),
      additions: Number(file?.additions) || 0,
      deletions: Number(file?.deletions) || 0,
      patch: boundedString(file?.patch, MAX_AI_PATCH_LENGTH),
    })),
    changedFileCount: fileDetails.length,
    categories: ownership.categories,
    visualPathHint: ownership.requiresVisualEvidence,
    visualEvidenceFound: visualEvidence.found,
    visualEvidenceTypes: visualEvidence.types,
  };
  const canonicalSource = JSON.stringify(source);
  const inputSha256 = crypto
    .createHash('sha256')
    .update(canonicalSource, 'utf8')
    .digest('hex');
  const fileLines = source.changedFiles.map((file) =>
    `- ${file.filename} (${file.status || 'modified'}, +${file.additions}/-${file.deletions})`);
  const patchSections = source.changedFiles
    .filter((file) => file.patch)
    .slice(0, MAX_AI_PATCH_FILES)
    .map((file) => `### ${file.filename}\n\n\`\`\`diff\n${file.patch}\n\`\`\``);

  return {
    inputSha256,
    context: [
      '# Deterministic PR evidence',
      '',
      `Input SHA-256: ${inputSha256}`,
      `PR number: ${source.number}`,
      `Draft: ${source.draft ? 'YES' : 'NO'}`,
      `Base ref: ${source.baseRef || 'Unknown'}`,
      `Head SHA: ${source.headSha || 'Unknown'}`,
      `Changed file count: ${source.changedFileCount}`,
      `Path categories: ${source.categories.join(', ') || 'Unknown'}`,
      `Visual path hint: ${source.visualPathHint ? 'YES' : 'NO'}`,
      `Visual evidence already present: ${source.visualEvidenceFound ? 'YES' : 'NO'}`,
      `Detected visual evidence types: ${source.visualEvidenceTypes.join(', ') || 'None'}`,
      '',
      '## Untrusted PR title',
      '',
      source.title || 'Not provided',
      '',
      '## Untrusted PR description',
      '',
      source.body || 'Not provided',
      '',
      `## Changed files (${Math.min(source.changedFiles.length, MAX_AI_FILE_COUNT)} shown)`,
      '',
      ...(fileLines.length ? fileLines : ['- None']),
      '',
      '## Bounded patch excerpts',
      '',
      ...(patchSections.length ? patchSections : ['No patch excerpts were available.']),
      '',
    ].join('\n'),
  };
}

export function extractAiAssessmentFromAgentOutput(output) {
  const item = output?.items?.find(
    (candidate) => candidate?.type === 'publish_pr_intake',
  );
  if (!item) {
    throw new Error('The agent did not provide a PR intake assessment');
  }
  const assessment = normalizeAiAssessment(item);
  if (!/^[a-f0-9]{64}$/.test(assessment.inputSha256)) {
    throw new Error('The agent did not provide a valid PR evidence hash');
  }
  if (!boundedString(item.summary, 800)) {
    throw new Error('The agent did not provide a PR summary');
  }
  if (![
    'REQUIRED',
    'RECOMMENDED',
    'NOT_NEEDED',
  ].includes(boundedString(item.visual_evidence_requirement, 40).toUpperCase())) {
    throw new Error('The agent provided an invalid visual-evidence requirement');
  }
  if (!boundedString(item.visual_evidence_reason, 500)) {
    throw new Error('The agent did not explain the visual-evidence requirement');
  }
  return assessment;
}

export async function preparePullRequestAiContext({ api, event, outputPath }) {
  const pullNumber = Number(event?.pull_request?.number);
  if (!Number.isSafeInteger(pullNumber) || pullNumber <= 0) {
    throw new Error('The GitHub event payload must contain a pull request');
  }
  const pullRequest = await api.getPullRequest(pullNumber);
  const files = await listAllPullRequestFileDetails(api, pullNumber);
  const result = buildPullRequestAiContext({ pullRequest, files });
  await fs.writeFile(outputPath, result.context, 'utf8');
  return {
    issueNumber: Number(pullRequest.number),
    inputSha256: result.inputSha256,
    outputPath,
  };
}

function parseIssueLabels(issue) {
  return Array.isArray(issue?.labels)
    ? issue.labels
      .map((entry) => typeof entry === 'string' ? entry : entry?.name)
      .filter((entry) => typeof entry === 'string' && entry.trim().length > 0)
    : [];
}

export async function runPullRequestIntake({ api, event, aiAssessment = null }) {
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
  const pullRequest = await api.getPullRequest(pullNumber);
  const issueNumber = pullNumber;

  const fileDetails = await listAllPullRequestFileDetails(api, issueNumber);
  const changedPaths = changedPathsFromFileDetails(fileDetails);
  const currentAiContext = buildPullRequestAiContext({
    pullRequest,
    files: fileDetails,
  });
  const normalizedAiAssessment = normalizeAiAssessment(
    aiAssessment,
    classifyChangedPaths(changedPaths).requiresVisualEvidence,
  );
  if (
    aiAssessment
    && !/^[a-f0-9]{64}$/.test(normalizedAiAssessment.inputSha256)
  ) {
    throw new Error('The agent assessment is missing a valid PR evidence hash');
  }
  if (
    normalizedAiAssessment.inputSha256
    && normalizedAiAssessment.inputSha256 !== currentAiContext.inputSha256
  ) {
    throw new Error('The agent assessment does not match the current pull request state');
  }
  const issue = await api.getIssue(issueNumber);
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
    verifiedClosingIssues: closingReferenceVerification.validReferences,
    invalidClosingIssues: closingReferenceVerification.invalidReferences,
    aiAssessment: normalizedAiAssessment,
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
  const desiredManagedLabels = [
    ...(report.readyForReview ? [READY_FOR_REVIEW_LABEL] : []),
    ...(report.needsAuthorFeedback ? [NEEDS_AUTHOR_FEEDBACK_LABEL] : []),
  ];
  const labelPlan = planManagedLabelChanges(
    parseIssueLabels(issue),
    desiredManagedLabels,
    [
      READY_FOR_REVIEW_LABEL,
      NEEDS_AUTHOR_FEEDBACK_LABEL,
    ],
  );

  await syncManagedLabels(api, issueNumber, labelPlan);
  const commentResult = await upsertCanonicalComment({
    api,
    issueNumber,
    body: renderIntakeComment(report, feedbackSince),
    comments,
  });

  return {
    issueNumber,
    changedPathCount: changedPaths.length,
    labelPlan,
    commentResult: {
      operation: commentResult.operation,
      commentId: commentResult.comment?.id ?? null,
      deletedExtraComments: commentResult.deletedExtraComments,
    },
    requiresVisualEvidence: report.requiresVisualEvidence,
    visualEvidenceRequirement: report.aiAssessment.visualEvidenceRequirement,
    visualEvidenceFound: report.visualEvidence.found,
    mergeConflict: report.mergeConflict,
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

  const prepareContextIndex = process.argv.indexOf('--prepare-ai-context');
  const publishOutputIndex = process.argv.indexOf('--publish-agent-output');
  let result;
  if (prepareContextIndex >= 0) {
    const outputPath = process.argv[prepareContextIndex + 1];
    if (!outputPath) {
      throw new Error('The AI context output path is required');
    }
    result = await preparePullRequestAiContext({ api, event, outputPath });
  } else if (publishOutputIndex >= 0) {
    const outputPath = process.argv[publishOutputIndex + 1];
    if (!outputPath) {
      throw new Error('The agent output path is required');
    }
    const output = JSON.parse(await fs.readFile(outputPath, 'utf8'));
    result = await runPullRequestIntake({
      api,
      event,
      aiAssessment: extractAiAssessmentFromAgentOutput(output),
    });
  } else {
    result = await runPullRequestIntake({ api, event });
  }
  console.log(JSON.stringify(result, null, 2));
}

const ENTRYPOINT = fileURLToPath(import.meta.url);
if (process.argv[1] && path.resolve(process.argv[1]) === ENTRYPOINT) {
  main().catch((error) => {
    console.error(error instanceof Error ? error.stack : String(error));
    process.exitCode = 1;
  });
}
