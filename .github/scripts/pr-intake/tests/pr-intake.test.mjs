import assert from 'node:assert/strict';
import fs from 'node:fs';
import test from 'node:test';

import {
  ACTIONS_BOT_ID,
  ACTIONS_BOT_LOGIN,
  CANONICAL_MARKER,
  NEEDS_AUTHOR_FEEDBACK_LABEL,
  READY_FOR_REVIEW_LABEL,
  ApiError,
  buildIntakeReport,
  classifyChangedPaths,
  deleteCanonicalComments,
  deriveProductLabelsFromPaths,
  deriveVisualAssessment,
  determineFeedbackSince,
  findClosingIssueReferences,
  findVisualEvidence,
  getPullRequestWithMergeability,
  hasMergeConflict,
  isMergeabilityKnown,
  parseFeedbackSince,
  planManagedLabelChanges,
  renderAllClearComment,
  renderIntakeComment,
  runPullRequestIntake,
  selectCanonicalComment,
  upsertCanonicalComment,
  verifyClosingIssueReferences,
} from '../pr-intake.mjs';

function botComment(id, body) {
  return {
    id,
    body,
    user: {
      login: ACTIONS_BOT_LOGIN,
      id: ACTIONS_BOT_ID,
      type: 'Bot',
    },
  };
}

test('workflow runs product labeling for drafts and handles draft transitions', () => {
  const workflow = fs.readFileSync(
    new URL('../../../workflows/pr-intake.yml', import.meta.url),
    'utf8',
  );

  assert.equal(READY_FOR_REVIEW_LABEL, 'Ready for review');
  assert.doesNotMatch(workflow, /github\.event\.pull_request\.draft == false/);
  assert.match(workflow, /- opened/);
  assert.match(workflow, /- edited/);
  assert.match(workflow, /- synchronize/);
  assert.match(workflow, /ref: \$\{\{ github\.event\.repository\.default_branch \}\}/);
  assert.match(workflow, /- ready_for_review/);
  assert.match(workflow, /- converted_to_draft/);
});

class MockApi {
  constructor({
    comments = [],
    issues = [],
    pullRequests = [],
    files = [],
    pullRequestQueue = null,
  } = {}) {
    this.comments = comments;
    this.issues = issues;
    this.pullRequests = pullRequests;
    this.files = files;
    this.pullRequestQueue = pullRequestQueue;
    this.created = 0;
    this.updated = 0;
    this.deleted = [];
    this.addedLabels = [];
    this.removedLabels = [];
    this.getIssueCalls = 0;
    this.getPullRequestCalls = 0;
    this.listPullRequestFilesCalls = 0;
    this.nextCommentId = 1000;
  }

  async listIssueComments(_issueNumber, page) {
    return page === 1 ? this.comments : [];
  }

  async createIssueComment(_issueNumber, body) {
    this.created += 1;
    const comment = botComment(this.nextCommentId, body);
    this.nextCommentId += 1;
    this.comments.push(comment);
    return comment;
  }

  async updateIssueComment(commentId, body) {
    this.updated += 1;
    const comment = this.comments.find((entry) => entry.id === commentId);
    comment.body = body;
    return comment;
  }

  async deleteIssueComment(commentId) {
    this.deleted.push(commentId);
    this.comments = this.comments.filter((entry) => entry.id !== commentId);
    return null;
  }

  async getIssue(issueNumber) {
    this.getIssueCalls += 1;
    const issue = this.issues.find((entry) => entry.number === issueNumber);
    if (!issue) {
      throw new ApiError('Not Found', 404);
    }
    return issue;
  }

  async getPullRequest(issueNumber) {
    this.getPullRequestCalls += 1;
    if (Array.isArray(this.pullRequestQueue) && this.pullRequestQueue.length) {
      return this.pullRequestQueue.shift();
    }
    return this.pullRequests.find((entry) => entry.number === issueNumber)
      ?? { number: issueNumber, draft: false };
  }

  async listPullRequestFiles(_pullNumber, page) {
    this.listPullRequestFilesCalls += 1;
    return page === 1 ? this.files : [];
  }

  async addLabels(issueNumber, labels) {
    this.addedLabels.push({ issueNumber, labels });
    return null;
  }

  async removeLabel(issueNumber, label) {
    this.removedLabels.push({ issueNumber, label });
    return null;
  }
}

test('changed paths provide a visual hint for product UI files', () => {
  const report = classifyChangedPaths([
    'src/settings-ui/Settings.UI/SettingsXAML/Views/DashboardPage.xaml',
    'doc/devdocs/core/architecture.md',
  ]);

  assert.equal(report.requiresVisualEvidence, true);
  assert.deepEqual(report.visualCandidatePaths, [
    'src/settings-ui/Settings.UI/SettingsXAML/Views/DashboardPage.xaml',
  ]);
});

test('docs-only changes do not require visual evidence', () => {
  const report = classifyChangedPaths([
    'README.md',
    'doc/devdocs/core/architecture.md',
  ]);

  assert.equal(report.requiresVisualEvidence, false);
  assert.deepEqual(report.categories, ['docs']);
});

test('path product labels use longest prefixes and suppress Settings side effects', () => {
  assert.deepEqual(
    deriveProductLabelsFromPaths([
      'src/modules/MouseUtils/MouseJump/MouseJump.Common/Helpers.cs',
      'src/settings-ui/Settings.UI/SettingsXAML/Views/MouseJumpPage.xaml',
    ]),
    ['Product-Mouse Jump'],
  );
  assert.deepEqual(
    deriveProductLabelsFromPaths(['src/modules/cmdpal/src/App/App.xaml']),
    ['Product-Command Palette'],
  );
  assert.deepEqual(
    deriveProductLabelsFromPaths(['src/modules/AltWindowCycle/AltWindowCycle.cpp']),
    ['Product-Window Hopper'],
  );
  assert.deepEqual(
    deriveProductLabelsFromPaths(
      ['src/settings-ui/Settings.UI/SettingsXAML/Views/DashboardPage.xaml'],
      ['Product-FancyZones'],
    ),
    [],
  );
});

test('path product labels cover module and shared roots additively', () => {
  assert.deepEqual(deriveProductLabelsFromPaths([
    'src/modules/AdvancedPaste/AdvancedPaste/Helpers/UserSettings.cs',
    'src/modules/cmdpal/ext/WindowWalker/ListPage.cs',
    'src/settings-ui/Settings.UI/ViewModels/AdvancedPasteViewModel.cs',
    'src/runner/main.cpp',
    'src/common/utils/helpers.cpp',
    'src/modules/UnknownModule/main.cpp',
  ]), [
    'Product-Advanced Paste',
    'Product-Command Palette',
  ]);
  assert.deepEqual(
    deriveProductLabelsFromPaths(['src/common/utils/helpers.cpp']),
    ['Product-General'],
  );
  assert.deepEqual(
    deriveProductLabelsFromPaths(
      ['src/runner/main.cpp'],
      ['Product-FancyZones'],
    ),
    [],
  );
});

test('mouse utility paths map to specific products before the umbrella label', () => {
  assert.deepEqual(deriveProductLabelsFromPaths([
    'src/modules/MouseUtils/CursorWrap/CursorWrap.cpp',
    'src/modules/MouseUtils/MouseJump.Common/Helpers.cs',
    'src/modules/MouseUtils/MouseUtils.UITests/TestHelpers.cs',
  ]), [
    'Product-Cursor Wrap',
    'Product-Mouse Jump',
    'Product-Mouse Utilities',
  ]);
});

test('every current module root has a deterministic product mapping', () => {
  const modulesUrl = new URL('../../../../src/modules/', import.meta.url);
  const moduleRoots = fs.readdirSync(modulesUrl, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name);
  const unmappedRoots = moduleRoots.filter(
    (root) => deriveProductLabelsFromPaths([`src/modules/${root}/placeholder.cpp`]).length === 0,
  );

  assert.deepEqual(unmappedRoots, []);
});

test('deriveVisualAssessment maps the path hint to a requirement', () => {
  assert.deepEqual(deriveVisualAssessment(true).visualEvidenceRequirement, 'REQUIRED');
  assert.deepEqual(deriveVisualAssessment(false).visualEvidenceRequirement, 'NOT_NEEDED');
  assert.match(deriveVisualAssessment(true).visualEvidenceReason, /product UI/);
});

test('merge conflicts are detected from GitHub mergeability fields', () => {
  assert.equal(hasMergeConflict({ mergeable: false }), true);
  assert.equal(hasMergeConflict({ mergeable_state: 'dirty' }), true);
  assert.equal(hasMergeConflict({ mergeStateStatus: 'CONFLICTING' }), true);
  assert.equal(hasMergeConflict({ mergeable: true, mergeable_state: 'clean' }), false);
  assert.equal(hasMergeConflict({ mergeable: null, mergeable_state: 'unknown' }), false);
});

test('mergeability is only known once GitHub reports a definite state', () => {
  assert.equal(isMergeabilityKnown({ mergeable: true }), true);
  assert.equal(isMergeabilityKnown({ mergeable: false }), true);
  assert.equal(isMergeabilityKnown({ mergeable_state: 'clean' }), true);
  assert.equal(isMergeabilityKnown({ mergeable: null, mergeable_state: 'unknown' }), false);
  assert.equal(isMergeabilityKnown({ mergeable: null }), false);
});

test('getPullRequestWithMergeability retries while mergeability is unknown', async () => {
  const api = new MockApi({
    pullRequestQueue: [
      { number: 7, mergeable: null, mergeable_state: 'unknown' },
      { number: 7, mergeable: null, mergeable_state: 'unknown' },
      { number: 7, mergeable: true, mergeable_state: 'clean' },
    ],
  });

  const result = await getPullRequestWithMergeability(api, 7, { delayMs: 0 });
  assert.equal(result.mergeabilityKnown, true);
  assert.equal(result.pullRequest.mergeable, true);
  assert.equal(api.getPullRequestCalls, 3);
});

test('getPullRequestWithMergeability gives up after the attempt cap', async () => {
  const api = new MockApi({
    pullRequestQueue: [
      { number: 7, mergeable: null, mergeable_state: 'unknown' },
      { number: 7, mergeable: null, mergeable_state: 'unknown' },
    ],
  });

  const result = await getPullRequestWithMergeability(api, 7, {
    maxAttempts: 2,
    delayMs: 0,
  });
  assert.equal(result.mergeabilityKnown, false);
  assert.equal(api.getPullRequestCalls, 2);
});

test('closing issue parsing finds supported keywords and de-duplicates issue numbers', () => {
  const references = findClosingIssueReferences(
    'Fixes #12\nResolved: #12\nCloses owner/repo#44',
  );

  assert.deepEqual(references, [
    { keyword: 'fixes', issueNumber: 12, repositoryFullName: null },
    { keyword: 'closes', issueNumber: 44, repositoryFullName: 'owner/repo' },
  ]);
});

test('closing issue parsing caps the number of references it accepts', () => {
  const body = Array.from({ length: 50 }, (_unused, index) => `Closes #${index + 1}`).join('\n');
  const references = findClosingIssueReferences(body);
  assert.equal(references.length, 20);
});

test('closing issue verification is bounded and preserves reference order', async () => {
  const api = new MockApi({
    issues: Array.from({ length: 20 }, (_unused, index) => ({
      number: index + 1,
      title: `Issue ${index + 1}`,
    })),
  });
  const references = Array.from({ length: 20 }, (_unused, index) => ({
    keyword: 'closes',
    issueNumber: index + 1,
    repositoryFullName: null,
  }));

  const result = await verifyClosingIssueReferences({
    api,
    repositoryFullName: 'microsoft/PowerToys',
    references,
  });

  assert.equal(result.validReferences.length, 20);
  assert.deepEqual(
    result.validReferences.map((entry) => entry.issueNumber),
    references.map((entry) => entry.issueNumber),
  );
});

test('closing issue verification accepts local issues and rejects pull requests, other repos, and 404s', async () => {
  const api = new MockApi({
    issues: [
      { number: 12, title: 'Tracked bug' },
      { number: 55, title: 'Feature PR', pull_request: { url: 'https://example.test/pr/55' } },
    ],
  });

  const result = await verifyClosingIssueReferences({
    api,
    repositoryFullName: 'microsoft/PowerToys',
    references: [
      { keyword: 'closes', issueNumber: 12, repositoryFullName: null },
      { keyword: 'fixes', issueNumber: 55, repositoryFullName: null },
      { keyword: 'resolves', issueNumber: 99, repositoryFullName: null },
      { keyword: 'closes', issueNumber: 44, repositoryFullName: 'other/repo' },
    ],
  });

  assert.deepEqual(result.validReferences, [
    { keyword: 'closes', issueNumber: 12, repositoryFullName: null, title: 'Tracked bug' },
  ]);
  assert.deepEqual(result.invalidReferences, [
    { keyword: 'fixes', issueNumber: 55, repositoryFullName: null, reason: 'pull-request' },
    { keyword: 'resolves', issueNumber: 99, repositoryFullName: null, reason: 'not-found' },
    { keyword: 'closes', issueNumber: 44, repositoryFullName: 'other/repo', reason: 'different-repository' },
  ]);
});

test('visual evidence detection recognizes markdown, attachments, and video links', () => {
  const evidence = findVisualEvidence(`
![Screenshot](https://example.com/screenshot.png)
https://github.com/user-attachments/assets/12345678-1234-1234-1234-123456789abc
https://www.youtube.com/watch?v=demo123
`);

  assert.equal(evidence.found, true);
  assert.deepEqual(evidence.types, [
    'GitHub user-attachment URL',
    'Markdown image',
    'Recognized video link',
  ]);
});

test('non-visual GitHub file attachments do not satisfy visual evidence', () => {
  const evidence = findVisualEvidence(`
https://github.com/user-attachments/files/1234/PowerToysReport_demo.zip
![fake](https://github.com/user-attachments/files/1234/PowerToysReport_demo.zip)
<img src="https://github.com/user-attachments/files/1234/PowerToysReport_demo.zip">
`);

  assert.equal(evidence.found, false);
  assert.deepEqual(evidence.types, []);
});

test('label plan removes only managed lifecycle labels', () => {
  const plan = planManagedLabelChanges(
    ['Product-FancyZones', NEEDS_AUTHOR_FEEDBACK_LABEL],
    [READY_FOR_REVIEW_LABEL],
    [READY_FOR_REVIEW_LABEL, NEEDS_AUTHOR_FEEDBACK_LABEL],
  );

  assert.deepEqual(plan, {
    add: [READY_FOR_REVIEW_LABEL],
    remove: [NEEDS_AUTHOR_FEEDBACK_LABEL],
  });
});

test('label plan migrates the legacy review label', () => {
  const plan = planManagedLabelChanges(
    ['Product-FancyZones', 'Needs-Review'],
    [READY_FOR_REVIEW_LABEL],
    [READY_FOR_REVIEW_LABEL, NEEDS_AUTHOR_FEEDBACK_LABEL, 'Needs-Review'],
  );

  assert.deepEqual(plan, {
    add: [READY_FOR_REVIEW_LABEL],
    remove: ['Needs-Review'],
  });
});

test('incomplete comment mentions the author and shows only actionable bullets', () => {
  const report = buildIntakeReport({
    changedPaths: ['src/settings-ui/Settings.UI/SettingsXAML/Views/DashboardPage.xaml'],
    body: '',
    repositoryHtmlUrl: 'https://github.com/microsoft/PowerToys',
    baseRef: 'main',
    authorLogin: 'alice',
    verifiedClosingIssues: [],
    invalidClosingIssues: [
      { keyword: 'closes', issueNumber: 999, repositoryFullName: null, reason: 'not-found' },
    ],
  });

  const body = renderIntakeComment(report, '2026-08-05T10:00:00.000Z');
  assert.match(body, /^<!-- powertoys-pr-intake:canonical:v1 -->/);
  assert.match(body, /## 🧭 PR intake/);
  assert.match(body, /Visual evidence:\*\* Required/);
  assert.match(body, /@alice, please update/);
  assert.match(body, /invalid closing reference `#999` \(not found\)/);
  assert.match(body, /Closes #123/);
  assert.match(body, /Replace the invalid closing reference/);
  assert.match(body, /screenshot, GIF, or video/);
  assert.match(body, /no author response within 7 days/);
  assert.equal(parseFeedbackSince(body), '2026-08-05T10:00:00.000Z');
  assert.match(body, /\[contribution guide\]\(https:\/\/github\.com\/microsoft\/PowerToys\/blob\/main\/CONTRIBUTING\.md\)/);
  assert.doesNotMatch(body, /Summary:|Ownership matches|Managed labels|Routing|Files scanned/);
});

test('complete intake renders the ready state without a summary line', () => {
  const report = buildIntakeReport({
    changedPaths: ['doc/devdocs/core/architecture.md'],
    body: 'Closes #12',
    repositoryHtmlUrl: 'https://github.com/microsoft/PowerToys',
    baseRef: 'main',
    authorLogin: 'alice',
    verifiedClosingIssues: [{ issueNumber: 12, title: 'Tracked issue' }],
    invalidClosingIssues: [],
  });

  const body = renderIntakeComment(report);
  assert.equal(report.readyForReview, true);
  assert.match(body, /## ✅ Ready for review/);
  assert.match(body, /Visual evidence:\*\* Not needed/);
  assert.doesNotMatch(body, /Summary:|@alice|contribution guide|Products|Routing/);
});

test('missing issue link is recommended without blocking readiness', () => {
  const report = buildIntakeReport({
    changedPaths: ['doc/devdocs/core/architecture.md'],
    body: 'Clarifies the contributor checklist.',
    repositoryHtmlUrl: 'https://github.com/microsoft/PowerToys',
    baseRef: 'main',
    authorLogin: 'alice',
    verifiedClosingIssues: [],
    invalidClosingIssues: [],
  });

  assert.equal(report.readyForReview, true);
  assert.equal(report.needsAuthorFeedback, false);
  assert.deepEqual(report.authorActions, []);
  assert.deepEqual(report.recommendations, [
    'Link the issue this PR fixes using a closing keyword such as `Closes #123`.',
  ]);
  assert.match(renderIntakeComment(report), /## Recommendation/);
});

test('merge conflict blocks readiness with an author action', () => {
  const report = buildIntakeReport({
    changedPaths: ['.github/workflows/issue-triage.md'],
    body: 'Closes #12',
    repositoryHtmlUrl: 'https://github.com/microsoft/PowerToys',
    baseRef: 'main',
    authorLogin: 'alice',
    mergeConflict: true,
    verifiedClosingIssues: [{ issueNumber: 12, title: 'Tracked issue' }],
    invalidClosingIssues: [],
  });

  assert.equal(report.readyForReview, false);
  assert.equal(report.needsAuthorFeedback, true);
  assert.deepEqual(report.authorActions, [
    'Resolve the merge conflicts with the target branch.',
  ]);
  assert.match(renderIntakeComment(report), /Resolve the merge conflicts/);
});

test('unknown mergeability holds readiness even when nothing else is flagged', () => {
  const report = buildIntakeReport({
    changedPaths: ['doc/devdocs/core/architecture.md'],
    body: 'Closes #12',
    repositoryHtmlUrl: 'https://github.com/microsoft/PowerToys',
    baseRef: 'main',
    authorLogin: 'alice',
    mergeabilityKnown: false,
    verifiedClosingIssues: [{ issueNumber: 12, title: 'Tracked issue' }],
    invalidClosingIssues: [],
  });

  assert.equal(report.readyForReview, false);
  assert.equal(report.needsAuthorFeedback, false);
  assert.deepEqual(report.authorActions, []);
});

test('product UI paths require visual evidence when none is present', () => {
  const report = buildIntakeReport({
    changedPaths: ['src/settings-ui/Settings.UI/SettingsXAML/Views/DashboardPage.xaml'],
    body: 'Closes #12',
    repositoryHtmlUrl: 'https://github.com/microsoft/PowerToys',
    baseRef: 'main',
    authorLogin: 'alice',
    verifiedClosingIssues: [{ issueNumber: 12, title: 'Tracked issue' }],
    invalidClosingIssues: [],
  });

  assert.equal(report.requiresVisualEvidence, true);
  assert.equal(report.visualAssessment.visualEvidenceRequirement, 'REQUIRED');
  assert.equal(report.readyForReview, false);
  assert.match(report.authorActions.join(' '), /screenshot, GIF, or video/);
});

test('product UI paths are satisfied when visual evidence is present', () => {
  const report = buildIntakeReport({
    changedPaths: ['src/settings-ui/Settings.UI/SettingsXAML/Views/DashboardPage.xaml'],
    body: 'Closes #12\n![screenshot](https://example.com/shot.png)',
    repositoryHtmlUrl: 'https://github.com/microsoft/PowerToys',
    baseRef: 'main',
    authorLogin: 'alice',
    verifiedClosingIssues: [{ issueNumber: 12, title: 'Tracked issue' }],
    invalidClosingIssues: [],
  });

  assert.equal(report.requiresVisualEvidence, true);
  assert.equal(report.readyForReview, true);
  assert.doesNotMatch(report.authorActions.join(' '), /screenshot/i);
});

test('draft PR remains incomplete until marked ready', () => {
  const report = buildIntakeReport({
    changedPaths: ['doc/devdocs/core/architecture.md'],
    body: 'Closes #12',
    repositoryHtmlUrl: 'https://github.com/microsoft/PowerToys',
    baseRef: 'main',
    authorLogin: 'alice',
    isDraft: true,
    verifiedClosingIssues: [{ issueNumber: 12, title: 'Tracked issue' }],
    invalidClosingIssues: [],
  });

  assert.equal(report.readyForReview, false);
  assert.equal(report.needsAuthorFeedback, false);
  assert.deepEqual(report.authorActions, ['Mark the pull request as ready for review.']);
});

test('readiness and author-feedback labels are mutually managed', () => {
  assert.deepEqual(
    planManagedLabelChanges(
      ['Product-Keyboard Manager'],
      [READY_FOR_REVIEW_LABEL],
      [READY_FOR_REVIEW_LABEL, NEEDS_AUTHOR_FEEDBACK_LABEL],
    ),
    { add: [READY_FOR_REVIEW_LABEL], remove: [] },
  );
  assert.deepEqual(
    planManagedLabelChanges(
      ['Product-Keyboard Manager', READY_FOR_REVIEW_LABEL],
      [],
      [READY_FOR_REVIEW_LABEL, NEEDS_AUTHOR_FEEDBACK_LABEL],
    ),
    { add: [], remove: [READY_FOR_REVIEW_LABEL] },
  );
  assert.deepEqual(
    planManagedLabelChanges(
      ['Product-FancyZones', READY_FOR_REVIEW_LABEL],
      [NEEDS_AUTHOR_FEEDBACK_LABEL],
      [READY_FOR_REVIEW_LABEL, NEEDS_AUTHOR_FEEDBACK_LABEL],
    ),
    { add: [NEEDS_AUTHOR_FEEDBACK_LABEL], remove: [READY_FOR_REVIEW_LABEL] },
  );
});

test('author activity resets the feedback window while bot activity does not', () => {
  const existingBody = renderIntakeComment(
    {
      readyForReview: false,
      needsAuthorFeedback: true,
      authorLogin: 'alice',
      authorActions: ['Update the PR.'],
      recommendations: [],
      contributingUrl: 'https://example.test/CONTRIBUTING.md',
      visualEvidence: { found: false, types: [] },
      visualAssessment: {
        visualEvidenceRequirement: 'NOT_NEEDED',
        visualEvidenceReason: 'The change is nonvisual.',
      },
    },
    '2026-08-01T10:00:00.000Z',
  );

  assert.equal(
    determineFeedbackSince({
      needsAuthorFeedback: true,
      existingCommentBody: existingBody,
      action: 'edited',
      senderLogin: 'alice',
      authorLogin: 'alice',
      now: '2026-08-05T10:00:00.000Z',
    }),
    '2026-08-05T10:00:00.000Z',
  );
  assert.equal(
    determineFeedbackSince({
      needsAuthorFeedback: true,
      existingCommentBody: existingBody,
      action: 'edited',
      senderLogin: ACTIONS_BOT_LOGIN,
      authorLogin: 'alice',
      now: '2026-08-05T10:00:00.000Z',
    }),
    '2026-08-01T10:00:00.000Z',
  );
});

test('canonical selection trusts only the GitHub Actions bot and chooses the oldest marker', () => {
  const body = `${CANONICAL_MARKER}\ncomment`;
  const selected = selectCanonicalComment([
    botComment(30, body),
    {
      id: 10,
      body,
      user: { login: ACTIONS_BOT_LOGIN, id: 99, type: 'Bot' },
    },
    {
      id: 5,
      body,
      user: { login: 'attacker', id: 1, type: 'User' },
    },
    botComment(20, body),
  ]);

  assert.equal(selected.canonical.id, 20);
  assert.deepEqual(selected.extras.map((entry) => entry.id), [30]);
});

test('canonical upsert updates the oldest trusted comment and deletes extras', async () => {
  const body = `${CANONICAL_MARKER}\nfirst`;
  const api = new MockApi({
    comments: [botComment(20, body), botComment(40, body)],
  });

  const result = await upsertCanonicalComment({
    api,
    issueNumber: 12,
    body: `${CANONICAL_MARKER}\nupdated`,
  });

  assert.equal(result.operation, 'updated');
  assert.equal(result.comment.id, 20);
  assert.deepEqual(result.deletedExtraComments, [40]);
  assert.equal(api.created, 0);
  assert.equal(api.updated, 1);
  assert.deepEqual(api.deleted, [40]);
  assert.equal(api.comments.length, 1);
});

test('canonical deletion removes only trusted intake comments', async () => {
  const body = `${CANONICAL_MARKER}\nfirst`;
  const untrusted = {
    id: 10,
    body,
    user: { login: 'attacker', id: 1, type: 'User' },
  };
  const api = new MockApi({
    comments: [untrusted, botComment(20, body), botComment(40, body)],
  });

  const deleted = await deleteCanonicalComments({ api, issueNumber: 12 });

  assert.deepEqual(deleted, [20, 40]);
  assert.deepEqual(api.deleted, [20, 40]);
  assert.deepEqual(api.comments, [untrusted]);
});

test('all-clear comment carries the canonical marker', () => {
  const body = renderAllClearComment();
  assert.match(body, /^<!-- powertoys-pr-intake:canonical:v1 -->/);
  assert.match(body, /All automated intake checks now pass/);
});

function intakeEvent(overrides = {}) {
  return {
    action: 'opened',
    repository: {
      full_name: 'microsoft/PowerToys',
      html_url: 'https://github.com/microsoft/PowerToys',
    },
    pull_request: { number: 100 },
    sender: { login: 'alice' },
    ...overrides,
  };
}

test('runPullRequestIntake stays silent on a clean PR with no prior comment', async () => {
  const api = new MockApi({
    issues: [{ number: 100, labels: [], title: 'PR' }],
    pullRequests: [{
      number: 100,
      draft: false,
      mergeable: true,
      mergeable_state: 'clean',
      body: 'Closes #12',
      base: { ref: 'main' },
      user: { login: 'alice' },
    }],
    files: [{ filename: 'doc/devdocs/core/architecture.md', status: 'modified' }],
  });
  api.issues.push({ number: 12, title: 'Tracked issue' });

  const result = await runPullRequestIntake({ api, event: intakeEvent() });
  assert.equal(result.commentResult.operation, 'skipped');
  assert.equal(api.created, 0);
  assert.equal(api.updated, 0);
  assert.deepEqual(result.labelPlan.add, [READY_FOR_REVIEW_LABEL]);
});

test('draft intake adds path labels, removes lifecycle labels, and deletes canonical comments', async () => {
  const canonical = botComment(50, `${CANONICAL_MARKER}\n## PR intake`);
  const untrusted = {
    id: 51,
    body: `${CANONICAL_MARKER}\nspoof`,
    user: { login: 'attacker', id: 1, type: 'User' },
  };
  const api = new MockApi({
    comments: [canonical, untrusted],
    issues: [{
      number: 100,
      labels: [
        READY_FOR_REVIEW_LABEL,
        NEEDS_AUTHOR_FEEDBACK_LABEL,
        'Product-FancyZones',
        'Area-Setup/Install',
      ],
      title: 'Draft PR',
    }],
    pullRequests: [{
      number: 100,
      draft: true,
      mergeable: true,
      mergeable_state: 'clean',
      body: '',
      base: { ref: 'main' },
      user: { login: 'alice' },
    }],
    files: [{ filename: 'src/modules/cmdpal/src/App/AppHost.cs', status: 'modified' }],
  });

  const result = await runPullRequestIntake({
    api,
    event: intakeEvent({ action: 'converted_to_draft' }),
  });

  assert.equal(result.skippedDraft, true);
  assert.deepEqual(result.labelPlan, {
    add: ['Product-Command Palette'],
    remove: [NEEDS_AUTHOR_FEEDBACK_LABEL, READY_FOR_REVIEW_LABEL],
  });
  assert.deepEqual(result.pathProductLabels, ['Product-Command Palette']);
  assert.equal(api.listPullRequestFilesCalls, 1);
  assert.deepEqual(api.addedLabels, [{
    issueNumber: 100,
    labels: ['Product-Command Palette'],
  }]);
  assert.deepEqual(api.deleted, [50]);
  assert.deepEqual(result.deletedCanonicalCommentIds, [50]);
  assert.deepEqual(api.comments, [untrusted]);
  assert.equal(api.created, 0);
  assert.equal(api.updated, 0);
});

test('non-draft intake adds deterministic product labels with lifecycle labels', async () => {
  const api = new MockApi({
    issues: [{
      number: 100,
      labels: ['Product-FancyZones'],
      title: 'Advanced Paste change',
    }],
    pullRequests: [{
      number: 100,
      draft: false,
      mergeable: true,
      mergeable_state: 'clean',
      body: 'Closes #12',
      base: { ref: 'main' },
      user: { login: 'alice' },
    }],
    files: [
      {
        filename: 'src/modules/AdvancedPaste/AdvancedPaste/Helpers/UserSettings.cs',
        status: 'modified',
      },
      {
        filename: 'src/settings-ui/Settings.UI/ViewModels/AdvancedPasteViewModel.cs',
        status: 'modified',
      },
    ],
  });
  api.issues.push({ number: 12, title: 'Tracked issue' });

  const result = await runPullRequestIntake({ api, event: intakeEvent() });

  assert.deepEqual(result.pathProductLabels, ['Product-Advanced Paste']);
  assert.deepEqual(result.labelPlan, {
    add: ['Product-Advanced Paste', READY_FOR_REVIEW_LABEL],
    remove: [],
  });
  assert.deepEqual(api.addedLabels, [{
    issueNumber: 100,
    labels: ['Product-Advanced Paste', READY_FOR_REVIEW_LABEL],
  }]);
});

test('runPullRequestIntake replaces a stale comment with an all-clear note when the PR is clean', async () => {
  const existing = botComment(50, `${CANONICAL_MARKER}\n## 🧭 PR intake\nplease update`);
  const api = new MockApi({
    comments: [existing],
    issues: [{ number: 100, labels: [NEEDS_AUTHOR_FEEDBACK_LABEL], title: 'PR' }],
    pullRequests: [{
      number: 100,
      draft: false,
      mergeable: true,
      mergeable_state: 'clean',
      body: 'Closes #12',
      base: { ref: 'main' },
      user: { login: 'alice' },
    }],
    files: [{ filename: 'doc/devdocs/core/architecture.md', status: 'modified' }],
  });
  api.issues.push({ number: 12, title: 'Tracked issue' });

  const result = await runPullRequestIntake({ api, event: intakeEvent() });
  assert.equal(result.commentResult.operation, 'updated');
  assert.match(existing.body, /All automated intake checks now pass/);
  assert.deepEqual(result.labelPlan.remove, [NEEDS_AUTHOR_FEEDBACK_LABEL]);
});

test('runPullRequestIntake posts a feedback comment when there are remarks', async () => {
  const api = new MockApi({
    issues: [{ number: 100, labels: [], title: 'PR' }],
    pullRequests: [{
      number: 100,
      draft: false,
      mergeable: true,
      mergeable_state: 'clean',
      body: 'No linked issue here.',
      base: { ref: 'main' },
      user: { login: 'alice' },
    }],
    files: [{
      filename: 'src/settings-ui/Settings.UI/SettingsXAML/Views/DashboardPage.xaml',
      status: 'modified',
    }],
  });

  const result = await runPullRequestIntake({ api, event: intakeEvent() });
  assert.equal(result.commentResult.operation, 'created');
  assert.equal(api.created, 1);
  assert.equal(result.readyForReview, false);
  assert.match(api.comments[0].body, /screenshot, GIF, or video/);
});
