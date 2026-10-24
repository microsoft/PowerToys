import unittest
from pathlib import Path


GITHUB_DIR = Path(__file__).resolve().parents[3]
WORKFLOW_SOURCE = GITHUB_DIR / "workflows" / "issue-triage.md"
WORKFLOW_LOCK = GITHUB_DIR / "workflows" / "issue-triage.lock.yml"


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

    def test_workflow_never_manages_version_labels(self):
        source = WORKFLOW_SOURCE.read_text(encoding="utf-8")
        generated = WORKFLOW_LOCK.read_text(encoding="utf-8")

        for workflow in (source, generated):
            self.assertNotIn("versionLabels", workflow)
            self.assertNotIn("desiredVersionLabel", workflow)
        self.assertRegex(source, r"never adds or removes\s+version labels")

    def test_closed_issues_are_gated_before_publication(self):
        source = WORKFLOW_SOURCE.read_text(encoding="utf-8")
        generated = WORKFLOW_LOCK.read_text(encoding="utf-8")

        self.assertIn("id: refresh", source)
        self.assertEqual(
            source.count("if: steps.refresh.outputs.should_process == 'true'"),
            3,
        )
        for workflow in (source, generated):
            self.assertIn("Issue is closed; ${action} was skipped.", workflow)
            self.assertIn(
                "currentIssue.closed_by?.login === 'github-actions[bot]'",
                workflow,
            )


if __name__ == "__main__":
    unittest.main()
