import importlib.util
import unittest
from pathlib import Path
from unittest import mock


MODULE_PATH = Path(__file__).parents[1] / "issue-context.py"
SPEC = importlib.util.spec_from_file_location("issue_context", MODULE_PATH)
CONTEXT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CONTEXT)


BUG_BODY = """### Microsoft PowerToys version

0.100.2

### Installation method

GitHub

### Area(s) with issue?

Keyboard Manager

### Steps to reproduce

1. Open Keyboard Manager.
2. Select Remap a shortcut.
3. Press a key and observe that the editor closes.

### Expected Behavior

The editor remains open.

### Actual Behavior

The editor exits.

### Upload Bug Report ZIP-file

_No response_
"""


class FakeApi:
    repository = "owner/repo"

    def __init__(self, results=None, comments=None, latest_release=None):
        self.results = results or []
        self.comments = comments or []
        self.latest_release = latest_release or {
            "tag_name": "v0.100.2",
            "prerelease": False,
        }
        self.queries = []

    def list_comments(self, _issue_number):
        return self.comments

    def list_labels(self):
        return [{"name": "Product-Keyboard Manager"}]

    def search_issues(self, query):
        self.queries.append(query)
        return self.results

    def latest_stable_powertoys_release(self):
        if isinstance(self.latest_release, Exception):
            raise self.latest_release
        return self.latest_release


class IssueContextTests(unittest.TestCase):
    def test_parses_bug_facts_without_ai(self):
        self.assertTrue(CONTEXT.is_bug_template(BUG_BODY))
        self.assertEqual(CONTEXT.parse_area(BUG_BODY), "Keyboard Manager")
        self.assertEqual(CONTEXT.parse_version(BUG_BODY), "0.100.2")
        self.assertEqual(CONTEXT.reproduction_quality(BUG_BODY), "SUFFICIENT")

    def test_version_status_distinguishes_outdated_current_and_preview(self):
        self.assertEqual(
            CONTEXT.version_status("0.99.1", "0.100.2"),
            "OUTDATED",
        )
        self.assertEqual(
            CONTEXT.version_status("0.100.2", "0.100.2"),
            "CURRENT",
        )
        self.assertEqual(
            CONTEXT.version_status("0.101.2211.0", "0.100.2"),
            "NEWER_THAN_STABLE",
        )
        self.assertEqual(
            CONTEXT.version_status("Not provided", "0.100.2"),
            "NOT_PROVIDED",
        )

    def test_latest_stable_release_failure_is_non_blocking(self):
        api = FakeApi(latest_release=RuntimeError("release lookup failed"))
        self.assertEqual(CONTEXT.latest_stable_version(api), "Unavailable")
        self.assertEqual(
            CONTEXT.version_status("0.99.1", "Unavailable"),
            "UNKNOWN",
        )

    def test_vague_reproduction_is_insufficient(self):
        body = BUG_BODY.replace(
            "1. Open Keyboard Manager.\n"
            "2. Select Remap a shortcut.\n"
            "3. Press a key and observe that the editor closes.",
            "It crashes.",
        )
        self.assertEqual(CONTEXT.reproduction_quality(body), "INSUFFICIENT")

    def test_concise_steps_with_separate_actual_behavior_are_sufficient(self):
        body = BUG_BODY.replace(
            "1. Open Keyboard Manager.\n"
            "2. Select Remap a shortcut.\n"
            "3. Press a key and observe that the editor closes.",
            "1. Open PowerToys Settings.\n2. Click General.",
        ).replace(
            "### Actual Behavior",
            "### ❌ Actual Behavior",
        ).replace(
            "The editor exits.",
            "Nothing happens and the current settings page remains open.",
        )
        self.assertEqual(
            CONTEXT.extract_section(body, "Actual Behavior"),
            "Nothing happens and the current settings page remains open.",
        )
        self.assertEqual(CONTEXT.reproduction_quality(body), "SUFFICIENT")

    def test_intermittent_failure_description_is_sufficient_for_intake(self):
        body = BUG_BODY.replace(
            "1. Open Keyboard Manager.\n"
            "2. Select Remap a shortcut.\n"
            "3. Press a key and observe that the editor closes.",
            "After some time of working, the mouse refuses to operate the "
            "other computer. It stays on my PC and does not jump over anymore.",
        ).replace(
            "The editor exits.",
            "_No response_",
        )
        self.assertEqual(CONTEXT.reproduction_quality(body), "SUFFICIENT")

    def test_passive_timed_crash_with_stack_trace_is_sufficient(self):
        body = BUG_BODY.replace(
            "1. Open Keyboard Manager.\n"
            "2. Select Remap a shortcut.\n"
            "3. Press a key and observe that the editor closes.",
            "A few minutes after Windows starts, while I am not actively "
            "using PowerToys, a Something went wrong message appears.\n\n"
            "at System.Windows.ThemeManager.OnSystemThemeChanged()\n"
            "at System.Windows.ExceptionWrapper.TryCatchWhen(...)",
        ).replace(
            "The editor exits.",
            "PowerToys crashes and displays the stack trace above.",
        )
        self.assertEqual(CONTEXT.reproduction_quality(body), "SUFFICIENT")

    def test_visual_ui_bug_does_not_require_report(self):
        body = BUG_BODY.replace(
            "The editor exits.",
            "After I updated PowerToys, the dialog layout has overlapping text "
            "and the button is misaligned.",
        )
        self.assertEqual(CONTEXT.bug_report_requirement(body), "OPTIONAL")

    def test_crash_or_startup_failure_requires_report(self):
        self.assertEqual(CONTEXT.bug_report_requirement(BUG_BODY), "REQUIRED")

    def test_update_failure_requires_report(self):
        body = BUG_BODY.replace(
            "The editor exits.",
            "The PowerToys update fails with an error and remains stuck.",
        ).replace(
            "3. Press a key and observe that the editor closes.",
            "3. Start the update and observe that it fails before completion.",
        )
        self.assertEqual(CONTEXT.bug_report_requirement(body), "REQUIRED")

    def test_other_actionable_bug_recommends_but_does_not_require_report(self):
        body = BUG_BODY.replace(
            "The editor exits.",
            "The shortcut is saved with the wrong key combination.",
        ).replace(
            "3. Press a key and observe that the editor closes.",
            "3. Press Ctrl+A and observe that Ctrl+B is saved instead.",
        )
        self.assertEqual(CONTEXT.bug_report_requirement(body), "RECOMMENDED")

    def test_non_bug_report_is_not_applicable(self):
        self.assertEqual(
            CONTEXT.bug_report_requirement("Please add a new utility."),
            "NOT_APPLICABLE",
        )

    def test_language_signal_detects_non_latin_author_prose(self):
        body = BUG_BODY.replace(
            "The editor exits.",
            "Редактор закрывается сразу после нажатия клавиши.",
        )
        self.assertEqual(
            CONTEXT.language_signal("Редактор закрывается", body),
            "NON_LATIN_TEXT",
        )

    def test_language_signal_ignores_template_and_hidden_import_marker(self):
        body = (
            BUG_BODY
            + "\n<!-- powertoys-bulk-import:source:microsoft/PowerToys#123 -->"
        )
        self.assertEqual(
            CONTEXT.language_signal("Keyboard Manager editor exits", body),
            "LATIN_SCRIPT_TEXT",
        )

    def test_language_signal_ignores_prefixed_template_headings(self):
        body = "\n".join(f"### ❌ {heading}" for heading in CONTEXT.BUG_HEADINGS)
        self.assertEqual(
            CONTEXT.language_signal("", body),
            "INSUFFICIENT_TEXT",
        )

    def test_language_signal_does_not_guess_from_short_technical_text(self):
        self.assertEqual(
            CONTEXT.language_signal("0x8007007E", "`Example.dll`"),
            "INSUFFICIENT_TEXT",
        )

    def test_hidden_import_marker_is_not_author_body_content(self):
        self.assertEqual(
            CONTEXT.author_body_status(
                "<!-- powertoys-bulk-import:source:microsoft/PowerToys#49813 -->"
            ),
            "EMPTY",
        )
        self.assertEqual(
            CONTEXT.author_body_status("Please add a new utility."),
            "PRESENT",
        )

    def test_queries_use_product_and_exact_technical_signals(self):
        queries = CONTEXT.build_queries(
            "owner/repo",
            "Keyboard Manager fails with 0x8007007E",
            "Unable to load Example.dll",
            "Product-Keyboard Manager",
        )
        rendered = "\n".join(queries)
        self.assertIn('label:"Product-Keyboard Manager"', rendered)
        self.assertIn('"0x8007007e"', rendered)
        self.assertIn('"example.dll"', rendered)

    def test_template_area_aliases_map_to_production_product_labels(self):
        labels = [
            {"name": "Product-FancyZones"},
            {"name": "Product-File Explorer"},
            {"name": "Product-General"},
        ]
        self.assertEqual(
            CONTEXT.product_label("FancyZones Editor", labels),
            "Product-FancyZones",
        )
        self.assertEqual(
            CONTEXT.product_label("File Explorer: Preview Pane", labels),
            "Product-File Explorer",
        )
        self.assertEqual(
            CONTEXT.product_label("System tray interaction", labels),
            "Product-General",
        )

    def test_context_redacts_report_attachment_urls(self):
        context = CONTEXT.render_context(
            {
                "number": 1,
                "title": "Keyboard Manager exits",
                "body": (
                    "https://github.com/user-attachments/files/2/"
                    "PowerToysReport_demo.zip"
                ),
            },
            {
                "issue_kind": "BUG",
                "area": "Keyboard Manager",
                "product_label": "Product-Keyboard Manager",
                "version": "0.100.2",
                "reproduction_quality": "SUFFICIENT",
            },
            [],
            [],
            "a" * 64,
        )
        self.assertIn("<PowerToysReport attachment>", context)
        self.assertNotIn("user-attachments", context)

    def test_ranking_prefers_matching_failure(self):
        current = {
            "title": "Keyboard Manager editor exits",
            "body": "Unable to load Example.dll with 0x8007007E",
            "labels": [{"name": "Product-Keyboard Manager"}],
        }
        exact = {
            "title": "Keyboard editor fails to open",
            "body": "Example.dll fails with 0x8007007E",
            "labels": [{"name": "Product-Keyboard Manager"}],
        }
        generic = {
            "title": "Keyboard layout request",
            "body": "Please add another layout.",
            "labels": [{"name": "Product-Keyboard Manager"}],
        }
        self.assertGreater(
            CONTEXT.candidate_score(current, exact, 2),
            CONTEXT.candidate_score(current, generic, 2),
        )

    def test_author_report_comment_is_relevant(self):
        event = {
            "issue": {"user": {"login": "alice"}},
            "comment": {
                "user": {"login": "alice"},
                "body": (
                    "Attached: https://github.com/user-attachments/files/2/"
                    "PowerToysReport_demo.zip"
                ),
            },
        }
        self.assertEqual(CONTEXT.should_process(event, event["comment"]), (True, False))

    def test_actions_bot_reopen_is_skipped(self):
        event = {
            "action": "reopened",
            "sender": {"login": "github-actions[bot]"},
            "issue": {"number": 10},
        }
        self.assertEqual(CONTEXT.should_process(event, None), (False, False))

    def test_unrelated_comment_writes_noop(self):
        event = {
            "action": "created",
            "issue": {
                "number": 10,
                "title": "Keyboard Manager exits",
                "body": BUG_BODY,
                "user": {"login": "alice"},
                "labels": [],
            },
            "comment": {
                "user": {"login": "bob"},
                "author_association": "NONE",
                "body": "I see this too.",
            },
        }
        with mock.patch.object(CONTEXT, "write_noop") as noop:
            _, _, should_process = CONTEXT.prepare(event, FakeApi())
        noop.assert_called_once()
        self.assertFalse(should_process)

    def test_pull_request_comment_writes_noop_without_api_reads(self):
        event = {
            "issue": {
                "number": 11,
                "pull_request": {"url": "https://api.github.com/repos/owner/repo/pulls/11"},
            }
        }
        api = FakeApi()
        with mock.patch.object(CONTEXT, "write_noop") as noop:
            _, _, should_process = CONTEXT.prepare(event, api)
        noop.assert_called_once()
        self.assertFalse(should_process)
        self.assertEqual(api.queries, [])

    def test_prepare_emits_bounded_ranked_candidates(self):
        issue = {
            "number": 10,
            "title": "Keyboard Manager editor exits with 0x8007007E",
            "body": BUG_BODY + "\nUnable to load Example.dll",
            "user": {"login": "alice"},
            "labels": [],
        }
        candidate = {
            "number": 3,
            "state": "open",
            "title": "Keyboard Manager editor fails",
            "body": "Unable to load Example.dll with 0x8007007E",
            "labels": [{"name": "Product-Keyboard Manager"}],
        }
        context, normalized, should_process = CONTEXT.prepare(
            {"action": "opened", "issue": issue},
            FakeApi(results=[candidate]),
        )
        self.assertIn('"number":3', context)
        self.assertIn("Input SHA-256:", context)
        self.assertIn("Latest stable PowerToys version: 0.100.2", context)
        self.assertIn("PowerToys version status: CURRENT", context)
        self.assertIn("Bug report requirement: REQUIRED", context)
        self.assertIn("Language signal: LATIN_SCRIPT_TEXT", context)
        self.assertIn("Author body status: PRESENT", context)
        self.assertEqual(normalized["issue"]["number"], 10)
        self.assertTrue(should_process)

    def test_candidate_retrieval_only_returns_older_issues(self):
        issue = {
            "number": 10,
            "title": "Keyboard Manager editor exits",
            "body": "Unable to load Example.dll with 0x8007007E",
            "labels": [{"name": "Product-Keyboard Manager"}],
        }
        older = {
            "number": 3,
            "state": "open",
            "title": "Keyboard Manager editor exits",
            "body": "Unable to load Example.dll with 0x8007007E",
            "labels": [{"name": "Product-Keyboard Manager"}],
        }
        newer = {**older, "number": 12}
        _, candidates = CONTEXT.retrieve_candidates(
            FakeApi(results=[older, newer]),
            issue,
            "Product-Keyboard Manager",
        )
        self.assertEqual([candidate["number"] for candidate in candidates], [3])


if __name__ == "__main__":
    unittest.main()
