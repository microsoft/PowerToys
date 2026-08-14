import assert from 'node:assert/strict';
import test from 'node:test';

import {
  ACTIONS_BOT_ID,
  ACTIONS_BOT_LOGIN,
  CANONICAL_MARKER,
  NEEDS_AUTHOR_FEEDBACK_LABEL,
  READY_FOR_REVIEW_LABEL,
  ApiError,
  buildIntakeReport,
  buildPullRequestAiContext,
  classifyChangedPaths,
  determineFeedbackSince,
  extractAiAssessmentFromAgentOutput,
  findClosingIssueReferences,
  findVisualEvidence,
  hasMergeConflict,
  parseFeedbackSince,
  planManagedLabelChanges,
  renderIntakeComment,
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

class MockApi {
  constructor({
    comments = [],
    issues = [],
    pullRequests = [],
  } = {}) {
    this.comments = comments;
    this.issues = issues;
    this.pullRequests = pullRequests;
    this.created = 0;
    this.updated = 0;
    this.deleted = [];
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
    const issue = this.issues.find((entry) => entry.number === issueNumber);
    if (!issue) {
      throw new ApiError('Not Found', 404);
    }
    return issue;
  }

  async getPullRequest(issueNumber) {
    return this.pullRequests.find((entry) => entry.number === issueNumber)
      ?? { number: issueNumber, draft: false };
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

test('merge conflicts are detected from GitHub mergeability fields', () => {
  assert.equal(hasMergeConflict({ mergeable: false }), true);
  assert.equal(hasMergeConflict({ mergeable_state: 'dirty' }), true);
  assert.equal(hasMergeConflict({ mergeStateStatus: 'CONFLICTING' }), true);
  assert.equal(hasMergeConflict({ mergeable: true, mergeable_state: 'clean' }), false);
  assert.equal(hasMergeConflict({ mergeable: null, mergeable_state: 'unknown' }), false);
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

test('AI context includes bounded PR evidence and a stable digest', () => {
  const pullRequest = {
    number: 42,
    title: 'Improve the layout editor',
    body: 'Closes #12',
    draft: false,
    base: { ref: 'main' },
    head: { sha: 'abc123' },
  };
  const files = [{
    filename: 'src/settings-ui/Settings.UI/SettingsXAML/Views/DashboardPage.xaml',
    status: 'modified',
    additions: 3,
    deletions: 1,
    patch: '@@ -1 +1 @@\n-Old\n+New',
  }];

  const first = buildPullRequestAiContext({ pullRequest, files });
  const second = buildPullRequestAiContext({ pullRequest, files });

  assert.equal(first.inputSha256, second.inputSha256);
  assert.match(first.inputSha256, /^[a-f0-9]{64}$/);
  assert.match(first.context, /Visual path hint: YES/);
  assert.match(first.context, /DashboardPage\.xaml/);
  assert.match(first.context, /```diff/);
});

test('agent output is normalized into a validated assessment', () => {
  const assessment = extractAiAssessmentFromAgentOutput({
    items: [{
      type: 'publish_pr_intake',
      input_sha256: 'A'.repeat(64),
      summary: 'Changes the visible layout editor call to action.',
      visual_evidence_requirement: 'required',
      visual_evidence_reason: 'The call to action is visible in the product UI.',
    }],
  });

  assert.deepEqual(assessment, {
    inputSha256: 'a'.repeat(64),
    summary: 'Changes the visible layout editor call to action.',
    visualEvidenceRequirement: 'REQUIRED',
    visualEvidenceReason: 'The call to action is visible in the product UI.',
  });
});

test('agent output rejects a missing PR evidence hash', () => {
  assert.throws(
    () => extractAiAssessmentFromAgentOutput({
      items: [{
        type: 'publish_pr_intake',
        input_sha256: '',
        summary: 'Updates the UI.',
        visual_evidence_requirement: 'REQUIRED',
        visual_evidence_reason: 'The visible layout changes.',
      }],
    }),
    /valid PR evidence hash/,
  );
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
    aiAssessment: {
      summary: 'Updates the visible FancyZones layout editor.',
      visualEvidenceRequirement: 'REQUIRED',
      visualEvidenceReason: 'The change alters visible product UI.',
    },
  });

  const body = renderIntakeComment(report, '2026-08-05T10:00:00.000Z');
  assert.match(body, /^<!-- powertoys-pr-intake:canonical:v1 -->/);
  assert.match(body, /## 🧭 PR intake/);
  assert.match(body, /Updates the visible FancyZones layout editor/);
  assert.match(body, /Visual evidence:\*\* Required/);
  assert.match(body, /@alice, please update/);
  assert.match(body, /invalid closing reference `#999` \(not found\)/);
  assert.match(body, /Closes #123/);
  assert.match(body, /Replace the invalid closing reference/);
  assert.match(body, /screenshot, GIF, or video/);
  assert.match(body, /no author response within 7 days/);
  assert.equal(parseFeedbackSince(body), '2026-08-05T10:00:00.000Z');
  assert.match(body, /\[contribution guide\]\(https:\/\/github\.com\/microsoft\/PowerToys\/blob\/main\/CONTRIBUTING\.md\)/);
  assert.doesNotMatch(body, /Ownership matches|Managed labels|Routing|Files scanned/);
});

test('complete intake renders the AI summary and ready state', () => {
  const report = buildIntakeReport({
    changedPaths: ['doc/devdocs/core/architecture.md'],
    body: 'Closes #12',
    repositoryHtmlUrl: 'https://github.com/microsoft/PowerToys',
    baseRef: 'main',
    authorLogin: 'alice',
    verifiedClosingIssues: [{ issueNumber: 12, title: 'Tracked issue' }],
    invalidClosingIssues: [],
    aiAssessment: {
      summary: 'Clarifies the docs-only contribution checklist.',
      visualEvidenceRequirement: 'NOT_NEEDED',
      visualEvidenceReason: 'The change only updates documentation.',
    },
  });

  const body = renderIntakeComment(report);
  assert.equal(report.readyForReview, true);
  assert.match(body, /## ✅ Ready for review/);
  assert.match(body, /Clarifies the docs-only contribution checklist/);
  assert.match(body, /Visual evidence:\*\* Not needed/);
  assert.doesNotMatch(body, /@alice|contribution guide|Products|Routing/);
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
    aiAssessment: {
      summary: 'Clarifies the docs-only contribution checklist.',
      visualEvidenceRequirement: 'NOT_NEEDED',
      visualEvidenceReason: 'The change only updates documentation.',
    },
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
    aiAssessment: {
      summary: 'Improves deterministic issue triage classification.',
      visualEvidenceRequirement: 'NOT_NEEDED',
      visualEvidenceReason: 'The change only updates repository automation.',
    },
  });

  assert.equal(report.readyForReview, false);
  assert.equal(report.needsAuthorFeedback, true);
  assert.deepEqual(report.authorActions, [
    'Resolve the merge conflicts with the target branch.',
  ]);
  assert.match(renderIntakeComment(report), /Resolve the merge conflicts/);
});

test('AI can override a product UI path when the actual change is nonvisual', () => {
  const report = buildIntakeReport({
    changedPaths: ['src/settings-ui/Settings.UI/SettingsXAML/Views/DashboardPage.xaml'],
    body: 'Closes #12',
    repositoryHtmlUrl: 'https://github.com/microsoft/PowerToys',
    baseRef: 'main',
    authorLogin: 'alice',
    verifiedClosingIssues: [{ issueNumber: 12, title: 'Tracked issue' }],
    invalidClosingIssues: [],
    aiAssessment: {
      summary: 'Renames an internal identifier without changing rendered UI.',
      visualEvidenceRequirement: 'NOT_NEEDED',
      visualEvidenceReason: 'The rendered interface is unchanged.',
    },
  });

  assert.equal(report.requiresVisualEvidence, true);
  assert.equal(report.aiAssessment.visualEvidenceRequirement, 'NOT_NEEDED');
  assert.equal(report.readyForReview, true);
});

test('AI can require visual evidence outside a preclassified UI path', () => {
  const report = buildIntakeReport({
    changedPaths: ['doc/devdocs/core/architecture.md'],
    body: 'Closes #12',
    repositoryHtmlUrl: 'https://github.com/microsoft/PowerToys',
    baseRef: 'main',
    authorLogin: 'alice',
    verifiedClosingIssues: [{ issueNumber: 12, title: 'Tracked issue' }],
    invalidClosingIssues: [],
    aiAssessment: {
      summary: 'Updates embedded UI guidance shown to contributors.',
      visualEvidenceRequirement: 'REQUIRED',
      visualEvidenceReason: 'The change updates a rendered visual example.',
    },
  });

  assert.equal(report.requiresVisualEvidence, false);
  assert.equal(report.readyForReview, false);
  assert.match(report.authorActions.join(' '), /screenshot, GIF, or video/);
});

test('recommended visual evidence remains nonblocking', () => {
  const report = buildIntakeReport({
    changedPaths: ['doc/devdocs/core/architecture.md'],
    body: 'Closes #12',
    repositoryHtmlUrl: 'https://github.com/microsoft/PowerToys',
    baseRef: 'main',
    authorLogin: 'alice',
    verifiedClosingIssues: [{ issueNumber: 12, title: 'Tracked issue' }],
    invalidClosingIssues: [],
    aiAssessment: {
      summary: 'Documents a user-facing workflow.',
      visualEvidenceRequirement: 'RECOMMENDED',
      visualEvidenceReason: 'A short recording would make the workflow easier to understand.',
    },
  });

  assert.equal(report.readyForReview, true);
  assert.doesNotMatch(report.authorActions.join(' '), /screenshot|video/i);
  assert.match(renderIntakeComment(report), /would help reviewers/);
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
    aiAssessment: {
      summary: 'Clarifies contributor guidance.',
      visualEvidenceRequirement: 'NOT_NEEDED',
      visualEvidenceReason: 'The change is documentation-only.',
    },
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
      aiAssessment: {
        summary: 'Updates the PR.',
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
