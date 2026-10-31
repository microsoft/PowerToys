import json
import re
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
        source = WORKFLOW_SOURCE.read_text(encoding="utf-8")
        generated = WORKFLOW_LOCK.read_text(encoding="utf-8")

        self.assertIn("  bash: false", source)
        self.assertIn("  cli-proxy: false", source)
        self.assertIn("--allow-tool safeoutputs", generated)
        self.assertIn("--deny-tool write", generated)
        self.assertNotRegex(generated, r"--allow-tool\s+\S*shell")
        self.assertNotIn("mcp_cli_tools_with_safeoutputs_prompt.md", generated)

    def test_missing_information_remains_a_typed_boolean(self):
        source = WORKFLOW_SOURCE.read_text(encoding="utf-8")
        generated = WORKFLOW_LOCK.read_text(encoding="utf-8")

        self.assertIn("never a quoted string", source)
        self.assertRegex(
            generated,
            r'"has_missing_information":\s*\{\s*"description":\s*"[^"]+",'
            r'\s*"type":\s*"boolean"',
        )

    def test_daily_guardrail_receives_conservative_per_run_budget(self):
        source = WORKFLOW_SOURCE.read_text(encoding="utf-8")
        generated = WORKFLOW_LOCK.read_text(encoding="utf-8")
        per_run = re.search(r"^max-ai-credits: (\d+)$", source, re.MULTILINE)
        daily = re.search(r"^max-daily-ai-credits: (\d+)$", source, re.MULTILINE)
        self.assertIsNotNone(per_run)
        self.assertIsNotNone(daily)
        self.assertGreater(int(per_run.group(1)), 0)
        guardrail = generated.split(
            "      - name: Check daily workflow token guardrail\n", 1
        )[1].split("      - name:", 1)[0]

        self.assertIn(f'GH_AW_MAX_AI_CREDITS: "{per_run.group(1)}"', guardrail)
        self.assertIn(f'GH_AW_MAX_DAILY_AI_CREDITS: "{daily.group(1)}"', guardrail)
        self.assertNotIn("continue-on-error:", guardrail)

    def test_compiler_and_runtime_versions_match(self):
        generated = WORKFLOW_LOCK.read_text(encoding="utf-8")
        metadata = json.loads(
            re.search(r"^# gh-aw-metadata: (.+)$", generated, re.MULTILINE).group(1)
        )
        manifest = json.loads(
            re.search(r"^# gh-aw-manifest: (.+)$", generated, re.MULTILINE).group(1)
        )
        setup = next(
            action
            for action in manifest["actions"]
            if action["repo"] == "github/gh-aw-actions/setup"
        )
        self.assertEqual(setup["version"], metadata["compiler_version"])
        runtime_pins = re.findall(
            r"^\s+uses: github/gh-aw-actions/setup@(\w+) # (\S+)$",
            generated,
            re.MULTILINE,
        )
        self.assertTrue(runtime_pins)
        self.assertEqual(set(runtime_pins), {(setup["sha"], setup["version"])})
        action_lock = json.loads(
            (GITHUB_DIR / "aw" / "actions-lock.json").read_text(encoding="utf-8")
        )
        self.assertEqual(
            action_lock["entries"][f'{setup["repo"]}@{setup["version"]}'],
            setup,
        )

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
