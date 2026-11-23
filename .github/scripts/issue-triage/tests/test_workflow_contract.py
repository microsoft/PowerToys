import json
import re
import unittest
from pathlib import Path


GITHUB_DIR = Path(__file__).resolve().parents[3]
WORKFLOW_SOURCE = GITHUB_DIR / "workflows" / "issue-triage.md"
WORKFLOW_LOCK = GITHUB_DIR / "workflows" / "issue-triage.lock.yml"


class WorkflowContractTests(unittest.TestCase):
    def test_setup_runtime_matches_workflow_compiler(self):
        generated = WORKFLOW_LOCK.read_text(encoding="utf-8")
        metadata = json.loads(generated.splitlines()[0].split(": ", 1)[1])
        setup_versions = re.findall(
            r"uses: github/gh-aw-actions/setup@[0-9a-f]{40} # (v\S+)",
            generated,
        )

        self.assertTrue(setup_versions)
        self.assertEqual(set(setup_versions), {metadata["compiler_version"]})

    def test_detector_installation_has_compiler_pinned_checksums(self):
        generated = WORKFLOW_LOCK.read_text(encoding="utf-8")
        install_command = next(
            line for line in generated.splitlines()
            if 'bash "${RUNNER_TEMP}/gh-aw/actions/install_threat_detect_binary.sh"' in line
        )

        for architecture in ("amd64", "arm64"):
            self.assertRegex(
                install_command,
                rf"--sha256-{architecture} [0-9a-f]{{64}}(?:\s|$)",
            )

    def test_detection_conclusion_receives_install_outcome_for_no_output_skip(self):
        generated = WORKFLOW_LOCK.read_text(encoding="utf-8")
        installation = generated.split(
            "      - name: Install threat-detect binary\n", 1
        )[1].split("      - name:", 1)[0]
        conclusion = generated.split(
            "      - name: Conclude threat detection\n", 1
        )[1].split("      - name:", 1)[0]

        self.assertIn("id: threat_detect_install", installation)
        self.assertIn(
            "if: always() && steps.detection_guard.outputs.run_detection == 'true'",
            installation,
        )
        self.assertIn(
            "RUN_DETECTION: ${{ steps.detection_guard.outputs.run_detection }}",
            conclusion,
        )
        self.assertIn(
            "THREAT_DETECT_INSTALL_OUTCOME: ${{ steps.threat_detect_install.outcome }}",
            conclusion,
        )

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
