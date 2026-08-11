import importlib.util
import json
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest import mock


MODULE_PATH = Path(__file__).parents[1] / "bug-report-analyzer.py"
SPEC = importlib.util.spec_from_file_location("bug_report_analyzer", MODULE_PATH)
ANALYZER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ANALYZER)


class BugReportAnalyzerTests(unittest.TestCase):
    def test_finds_last_power_toys_report_attachment(self):
        event = {
            "issue": {
                "body": (
                    "https://github.com/user-attachments/files/1/not-a-report.zip\n"
                    "https://github.com/user-attachments/files/2/"
                    "PowerToysReport_2026-08-04-10-00-00.zip"
                )
            }
        }
        self.assertEqual(
            ANALYZER.find_attachment_url(event),
            "https://github.com/user-attachments/files/2/"
            "PowerToysReport_2026-08-04-10-00-00.zip",
        )

    def test_rejects_path_traversal(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            zip_path = Path(temp_dir) / "unsafe.zip"
            with zipfile.ZipFile(zip_path, "w") as archive:
                archive.writestr("../secret.txt", "secret")
            with zipfile.ZipFile(zip_path) as archive:
                with self.assertRaises(ANALYZER.AnalysisRejected):
                    ANALYZER.validate_archive(archive)

    def test_rejects_encrypted_archives(self):
        entry = zipfile.ZipInfo("secret.txt")
        entry.flag_bits = 0x1
        archive = mock.Mock()
        archive.infolist.return_value = [entry]

        with self.assertRaises(ANALYZER.AnalysisRejected):
            ANALYZER.validate_archive(archive)

    def test_redacts_common_identifiers(self):
        text = (
            r"C:\Users\alice\AppData\Local email@example.com 10.0.0.8 "
            r"https://example.com/path token=abcdef "
            r"123e4567-e89b-42d3-a456-426614174000"
        )
        redacted = ANALYZER.redact(text)
        self.assertNotIn("alice", redacted)
        self.assertNotIn("email@example.com", redacted)
        self.assertNotIn("10.0.0.8", redacted)
        self.assertNotIn("https://example.com", redacted)
        self.assertNotIn("abcdef", redacted)
        self.assertNotIn("123e4567", redacted)

    def test_collects_relevant_error_signals_only(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            zip_path = Path(temp_dir) / "report.zip"
            with zipfile.ZipFile(zip_path, "w") as archive:
                archive.writestr(
                    "Keyboard Manager/WinUI3Editor/Logs/0.1/Log_2026-08-04.log",
                    "[Error] Failed to initialize mapping service\n"
                    "Unable to load DLL 'Example.dll': module could not be found\n",
                )
                archive.writestr(
                    "FancyZones/Logs/log_2026-08-04.log",
                    "[Error] This unrelated signal should be ignored\n",
                )
                archive.writestr(
                    "RunnerLogs/runner.log",
                    "[Error] get_power_toys_settings(): got malformed json\n",
                )
            with zipfile.ZipFile(zip_path) as archive:
                entries = ANALYZER.validate_archive(archive)
                signals = ANALYZER.collect_signals(archive, entries, "Keyboard Manager")
        rendered = "\n".join(signals)
        self.assertIn("Example.dll", rendered)
        self.assertRegex(rendered, r"Log_2026-08-04\.log:1:")
        self.assertNotIn("unrelated", rendered)
        self.assertNotIn("malformed json", rendered)

    def test_metadata_is_restricted_to_environment_versions(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            zip_path = Path(temp_dir) / "report.zip"
            with zipfile.ZipFile(zip_path, "w") as archive:
                archive.writestr(
                    "windows-version.txt",
                    "ProductName: Windows 11\n"
                    "BuildNumber: 26100\n"
                    "RegisteredOwner: Alice\n",
                )
                archive.writestr(
                    "dotnet-installation-info.txt",
                    "Host:\n"
                    "  Version: 9.0.0\n"
                    "User profile: C:\\Users\\alice\n"
                    "Microsoft.WindowsDesktop.App 9.0.0\n",
                )
                archive.writestr("monitor-info.txt", "DeviceName: private-monitor")
            with zipfile.ZipFile(zip_path) as archive:
                entries = ANALYZER.validate_archive(archive)
                metadata = ANALYZER.collect_metadata(archive, entries)
        rendered = "\n".join(f"{name}: {value}" for name, value in metadata)
        self.assertIn("BuildNumber: 26100", rendered)
        self.assertIn("Microsoft.WindowsDesktop.App", rendered)
        self.assertNotIn("Alice", rendered)
        self.assertNotIn("private-monitor", rendered)

    def test_analyze_event_never_includes_attachment_url(self):
        event = {
            "issue": {
                "body": (
                    "### Area(s) with issue?\n\nKeyboard Manager\n\n"
                    "https://github.com/user-attachments/files/2/"
                    "PowerToysReport_2026-08-04-10-00-00.zip"
                )
            }
        }
        with tempfile.NamedTemporaryFile(suffix=".zip", delete=False) as temp_zip:
            path = temp_zip.name
        try:
            with zipfile.ZipFile(path, "w") as archive:
                archive.writestr("windows-version.txt", "BuildNumber: 26100")
            with mock.patch.object(
                ANALYZER,
                "download_attachment",
                return_value=(path, "abc123"),
            ):
                context = ANALYZER.analyze_event(event)
            self.assertIn("Status: ANALYZED", context)
            self.assertIn("BuildNumber: 26100", context)
            self.assertNotIn("user-attachments", context)
        finally:
            Path(path).unlink(missing_ok=True)


if __name__ == "__main__":
    unittest.main()
