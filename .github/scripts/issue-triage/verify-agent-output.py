#!/usr/bin/env python3

import json
import re
import sys


def load_triage_item(agent_output):
    items = agent_output.get("items")
    if not isinstance(items, list):
        raise ValueError("Agent output does not contain an items array")
    matches = [
        item
        for item in items
        if isinstance(item, dict) and item.get("type") == "publish_triage_summary"
    ]
    if len(matches) != 1:
        raise ValueError("Agent output must contain exactly one triage summary")
    return matches[0]


def exact_string(item, name, expected):
    actual = item.get(name)
    if actual != expected:
        raise ValueError(f"{name} does not match deterministic evidence")
    return actual


def verify(item, evidence, bug_report_context):
    input_sha256 = str(item.get("input_sha256") or "").lower()
    if input_sha256 != evidence.get("input_sha256"):
        raise ValueError("input_sha256 does not match current issue content")

    suggested_area = exact_string(
        item,
        "suggested_area",
        evidence.get("suggested_area"),
    )
    powertoys_version = exact_string(
        item,
        "powertoys_version",
        evidence.get("powertoys_version"),
    )
    issue_kind = exact_string(item, "issue_kind", evidence.get("issue_kind"))
    reproduction_quality = exact_string(
        item,
        "reproduction_quality",
        evidence.get("reproduction_quality"),
    )
    bug_report_requirement = exact_string(
        item,
        "bug_report_requirement",
        evidence.get("bug_report_requirement"),
    )

    product_label = item.get("product_label")
    allowed_labels = set(evidence.get("allowed_product_labels") or [])
    deterministic_label = evidence.get("candidate_product_label")
    if deterministic_label != "None":
        if product_label != deterministic_label:
            raise ValueError("product_label does not match deterministic evidence")
    elif product_label != "None" and product_label not in allowed_labels:
        raise ValueError("product_label is outside the deterministic candidate set")

    try:
        duplicate_candidates = json.loads(item.get("duplicate_candidates_json"))
    except (TypeError, json.JSONDecodeError) as error:
        raise ValueError("duplicate_candidates_json is invalid") from error
    if not isinstance(duplicate_candidates, list) or len(duplicate_candidates) > 5:
        raise ValueError("duplicate_candidates_json must contain at most five items")
    allowed_duplicates = set(evidence.get("duplicate_candidate_numbers") or [])
    requested_duplicate_numbers = []
    for candidate in duplicate_candidates:
        if not isinstance(candidate, dict) or not isinstance(candidate.get("number"), int):
            raise ValueError("Each duplicate candidate must contain an integer number")
        number = candidate["number"]
        if number not in allowed_duplicates:
            raise ValueError("Duplicate candidate is outside deterministic retrieval results")
        requested_duplicate_numbers.append(number)

    status_match = re.search(
        r"^Status: (ANALYZED|NOT_FOUND|REJECTED)$",
        bug_report_context,
        re.MULTILINE,
    )
    bug_report_status = (
        status_match.group(1)
        if issue_kind == "BUG" and status_match
        else "NOT_APPLICABLE"
    )
    if item.get("bug_report_status") != bug_report_status:
        raise ValueError("bug_report_status does not match sanitized diagnostics")

    return {
        "input_sha256": input_sha256,
        "suggested_area": suggested_area,
        "product_label": product_label,
        "powertoys_version": powertoys_version,
        "issue_kind": issue_kind,
        "reproduction_quality": reproduction_quality,
        "bug_report_requirement": bug_report_requirement,
        "bug_report_status": bug_report_status,
        "requested_duplicate_numbers": requested_duplicate_numbers,
        "issue_author": evidence.get("issue_author"),
        "current_labels": evidence.get("current_labels") or [],
    }


def main():
    if len(sys.argv) != 5:
        print(
            "Usage: verify-agent-output.py AGENT_OUTPUT EVIDENCE_JSON "
            "BUG_REPORT_CONTEXT VERIFIED_OUTPUT",
            file=sys.stderr,
        )
        return 2
    agent_output_path, evidence_path, bug_context_path, output_path = sys.argv[1:]
    with open(agent_output_path, "r", encoding="utf-8") as input_file:
        item = load_triage_item(json.load(input_file))
    with open(evidence_path, "r", encoding="utf-8") as input_file:
        evidence = json.load(input_file)
    with open(bug_context_path, "r", encoding="utf-8") as input_file:
        bug_report_context = input_file.read()
    verified = verify(item, evidence, bug_report_context)
    with open(output_path, "w", encoding="utf-8", newline="\n") as output_file:
        json.dump(verified, output_file, ensure_ascii=True, sort_keys=True)
        output_file.write("\n")
    print("Verified agent output against fresh deterministic evidence")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
