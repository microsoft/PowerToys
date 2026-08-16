import unittest
from pathlib import Path


GITHUB_DIR = Path(__file__).resolve().parents[3]
WORKFLOW_SOURCE = GITHUB_DIR / "workflows" / "issue-triage.md"
WORKFLOW_LOCK = GITHUB_DIR / "workflows" / "issue-triage.lock.yml"
DEDUPE_DIGEST = GITHUB_DIR / "workflows" / "dedupe-digest.yml"
MANUAL_DEDUPE = GITHUB_DIR / "workflows" / "manual-batch-issue-deduplication.yml"


class WorkflowContractTests(unittest.TestCase):
    def test_agent_evidence_uses_shared_runtime_directory(self):
        source = WORKFLOW_SOURCE.read_text(encoding="utf-8")
        generated = WORKFLOW_LOCK.read_text(encoding="utf-8")

        for filename in (
            "issue-context.md",
            "triage-event.json",
            "bug-report-context.md",
        ):
            expected_path = f"/tmp/gh-aw/{filename}"
            self.assertIn(expected_path, source)
            self.assertIn(expected_path, generated)

    def test_copilot_can_publish_without_general_write_or_shell_access(self):
        generated = WORKFLOW_LOCK.read_text(encoding="utf-8")

        self.assertIn("shell(safeoutputs:*)", generated)
        self.assertIn("--deny-tool write", generated)
        for command in (
            "cat",
            "date",
            "echo",
            "grep",
            "head",
            "ls",
            "printf",
            "pwd",
            "sort",
            "tail",
            "uniq",
            "wc",
            "yq",
        ):
            self.assertIn(f"--deny-tool '\\''shell({command})'\\''", generated)

    def test_dedupe_workflows_restrict_canonical_candidates_to_open_issues(self):
        digest = DEDUPE_DIGEST.read_text(encoding="utf-8")
        manual = MANUAL_DEDUPE.read_text(encoding="utf-8")

        self.assertIn("resolveOpenIssueCandidates", digest)
        self.assertIn("live.pull_request || live.state !== 'open'", digest)
        self.assertIn(
            "const candidates = await resolveOpenIssueCandidates(",
            digest,
        )
        self.assertIn("state: open", manual)


if __name__ == "__main__":
    unittest.main()
