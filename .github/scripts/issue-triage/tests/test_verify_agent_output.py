import importlib.util
import pathlib
import unittest


SCRIPT = pathlib.Path(__file__).parents[1] / "verify-agent-output.py"
SPEC = importlib.util.spec_from_file_location("verify_agent_output", SCRIPT)
VERIFY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VERIFY)


class VerifyAgentOutputTests(unittest.TestCase):
    def setUp(self):
        self.evidence = {
            "input_sha256": "a" * 64,
            "suggested_area": "Screen Ruler",
            "candidate_product_label": "None",
            "allowed_product_labels": ["Product-Screen Ruler"],
            "powertoys_version": "0.100.0",
            "issue_kind": "BUG",
            "reproduction_quality": "SUFFICIENT",
            "bug_report_requirement": "OPTIONAL",
            "duplicate_candidate_numbers": [10, 20],
            "issue_author": "alice",
            "current_labels": [],
        }
        self.item = {
            "input_sha256": "a" * 64,
            "suggested_area": "Screen Ruler",
            "product_label": "Product-Screen Ruler",
            "powertoys_version": "0.100.0",
            "issue_kind": "BUG",
            "reproduction_quality": "SUFFICIENT",
            "bug_report_requirement": "OPTIONAL",
            "bug_report_status": "NOT_FOUND",
            "duplicate_candidates_json": (
                '[{"number":10,"reason":"Same failure","confidence":"HIGH"}]'
            ),
        }

    def test_accepts_only_deterministic_candidates(self):
        verified = VERIFY.verify(
            self.item,
            self.evidence,
            "Status: NOT_FOUND\n",
        )
        self.assertEqual(verified["product_label"], "Product-Screen Ruler")
        self.assertEqual(verified["requested_duplicate_numbers"], [10])

    def test_rejects_product_label_outside_candidate_set(self):
        self.item["product_label"] = "Product-FancyZones"
        with self.assertRaisesRegex(ValueError, "candidate set"):
            VERIFY.verify(self.item, self.evidence, "Status: NOT_FOUND\n")

    def test_rejects_duplicate_outside_retrieval_results(self):
        self.item["duplicate_candidates_json"] = (
            '[{"number":99,"reason":"Injected","confidence":"HIGH"}]'
        )
        with self.assertRaisesRegex(ValueError, "retrieval results"):
            VERIFY.verify(self.item, self.evidence, "Status: NOT_FOUND\n")

    def test_rejects_stale_input_hash(self):
        self.item["input_sha256"] = "b" * 64
        with self.assertRaisesRegex(ValueError, "current issue content"):
            VERIFY.verify(self.item, self.evidence, "Status: NOT_FOUND\n")


if __name__ == "__main__":
    unittest.main()
