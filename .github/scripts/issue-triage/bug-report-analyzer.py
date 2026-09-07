#!/usr/bin/env python3

import hashlib
import json
import os
import re
import stat
import sys
import tempfile
import urllib.parse
import urllib.request
import zipfile
from pathlib import PurePosixPath


MAX_DOWNLOAD_BYTES = 16 * 1024 * 1024
MAX_ARCHIVE_ENTRIES = 3000
MAX_UNCOMPRESSED_BYTES = 64 * 1024 * 1024
MAX_ENTRY_BYTES = 8 * 1024 * 1024
MAX_COMPRESSION_RATIO = 200
MAX_SOURCE_FILES = 24
MAX_SIGNAL_COUNT = 10
MAX_SIGNAL_CHARS = 9000
MAX_CONTEXT_CHARS = 18000

ATTACHMENT_PATTERN = re.compile(
    r"https://github\.com/user-attachments/files/\d+/"
    r"PowerToysReport_[A-Za-z0-9_.-]+\.zip",
    re.IGNORECASE,
)
ERROR_PATTERN = re.compile(
    r"\[(?:error|fatal|critical)\]|exception|unable to load|"
    r"could not be found|module could not be found|failed to|"
    r"0x[0-9a-f]{6,}",
    re.IGNORECASE,
)
METADATA_FILES = {
    "windows-version.txt",
    "dotnet-installation-info.txt",
}
PRODUCT_LOG_HINTS = {
    "fancyzones": ("fancyzones/",),
    "keyboardmanager": ("keyboard manager/", "keyboardmanager/"),
    "colorpicker": ("color picker/", "colorpicker/"),
    "powertoysrun": ("powertoys run/", "powertoysrun/", "launcher/"),
    "awake": ("awake/",),
    "mouseutilities": ("mouse utilities/", "mouseutilities/"),
}


class AnalysisRejected(Exception):
    pass


class RestrictedRedirectHandler(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        parsed = urllib.parse.urlparse(newurl)
        if parsed.scheme != "https" or parsed.hostname not in {
            "github.com",
            "objects.githubusercontent.com",
        }:
            raise AnalysisRejected("Attachment redirected to an unapproved host")
        return super().redirect_request(req, fp, code, msg, headers, newurl)


def find_attachment_url(event):
    issue = event.get("issue") if isinstance(event, dict) else None
    comment = event.get("comment") if isinstance(event, dict) else None
    text = "\n".join(
        value
        for value in [
            issue.get("body") if isinstance(issue, dict) else None,
            comment.get("body") if isinstance(comment, dict) else None,
        ]
        if isinstance(value, str)
    )
    matches = ATTACHMENT_PATTERN.findall(text)
    return matches[-1] if matches else None


def parse_issue_area(event):
    issue = event.get("issue") if isinstance(event, dict) else None
    body = issue.get("body") if isinstance(issue, dict) else ""
    if not isinstance(body, str):
        return "Unknown"
    match = re.search(
        r"^###\s+Area\(s\) with issue\?\s*$\s*(.+?)(?=^###|\Z)",
        body,
        re.IGNORECASE | re.MULTILINE | re.DOTALL,
    )
    if not match:
        return "Unknown"
    area = next(
        (line.strip() for line in match.group(1).splitlines() if line.strip()),
        "Unknown",
    )
    return area[:100] or "Unknown"


def validate_attachment_url(url):
    parsed = urllib.parse.urlparse(url)
    if (
        parsed.scheme != "https"
        or parsed.hostname != "github.com"
        or not parsed.path.startswith("/user-attachments/files/")
        or not ATTACHMENT_PATTERN.fullmatch(url)
    ):
        raise AnalysisRejected("Attachment URL is not an approved PowerToys report")


def download_attachment(url):
    validate_attachment_url(url)
    opener = urllib.request.build_opener(RestrictedRedirectHandler())
    request = urllib.request.Request(
        url,
        headers={"User-Agent": "microsoft-powertoys-issue-triage"},
    )
    digest = hashlib.sha256()
    temp_file = tempfile.NamedTemporaryFile(prefix="powertoys-report-", suffix=".zip", delete=False)
    try:
        with temp_file, opener.open(request, timeout=30) as response:
            content_length = response.headers.get("Content-Length")
            if content_length and int(content_length) > MAX_DOWNLOAD_BYTES:
                raise AnalysisRejected("Attachment exceeds the download size limit")
            total = 0
            while True:
                chunk = response.read(64 * 1024)
                if not chunk:
                    break
                total += len(chunk)
                if total > MAX_DOWNLOAD_BYTES:
                    raise AnalysisRejected("Attachment exceeds the download size limit")
                digest.update(chunk)
                temp_file.write(chunk)
        return temp_file.name, digest.hexdigest()
    except Exception:
        try:
            os.unlink(temp_file.name)
        except FileNotFoundError:
            pass
        raise


def validate_archive(archive):
    entries = archive.infolist()
    if not entries or len(entries) > MAX_ARCHIVE_ENTRIES:
        raise AnalysisRejected("Archive has an invalid number of entries")

    total_size = 0
    for entry in entries:
        path = PurePosixPath(entry.filename)
        unix_mode = entry.external_attr >> 16
        if (
            path.is_absolute()
            or ".." in path.parts
            or "\\" in entry.filename
            or stat.S_ISLNK(unix_mode)
            or entry.flag_bits & 0x1
        ):
            raise AnalysisRejected("Archive contains an unsafe entry")
        if entry.file_size > MAX_ENTRY_BYTES:
            raise AnalysisRejected("Archive contains an oversized entry")
        total_size += entry.file_size
        if total_size > MAX_UNCOMPRESSED_BYTES:
            raise AnalysisRejected("Archive exceeds the uncompressed size limit")
        if (
            entry.file_size > 0
            and entry.compress_size > 0
            and entry.file_size / entry.compress_size > MAX_COMPRESSION_RATIO
        ):
            raise AnalysisRejected("Archive contains a suspicious compression ratio")
    return entries


def decode_text(raw):
    for encoding in ("utf-8-sig", "utf-16", "cp1252"):
        try:
            return raw.decode(encoding)
        except UnicodeDecodeError:
            continue
    return raw.decode("utf-8", errors="replace")


def redact(text):
    value = text.replace("\x00", "")
    value = re.sub(
        r"(?i)\b[A-Z]:\\Users\\[^\\\s\"']+",
        r"<user-profile>",
        value,
    )
    value = re.sub(r"(?i)/(?:home|Users)/[^/\s\"']+", "/<user>", value)
    value = re.sub(r"\\\\[^\\\s]+\\", r"\\<server>\\", value)
    value = re.sub(
        r"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
        "<email>",
        value,
    )
    value = re.sub(
        r"(?i)\bhttps?://[^\s<>\"]+",
        "<url>",
        value,
    )
    value = re.sub(
        r"\b(?:25[0-5]|2[0-4]\d|1?\d?\d)"
        r"(?:\.(?:25[0-5]|2[0-4]\d|1?\d?\d)){3}\b",
        "<ip-address>",
        value,
    )
    value = re.sub(
        r"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-"
        r"[89ab][0-9a-f]{3}-[0-9a-f]{12}\b",
        "<guid>",
        value,
    )
    value = re.sub(r"\bS-1-5-(?:\d+-){1,14}\d+\b", "<sid>", value)
    value = re.sub(
        r"(?i)\b(token|secret|password|securitykey)\b\s*[:=]\s*[^\s,;]+",
        r"\1=<redacted>",
        value,
    )
    value = re.sub(
        r"(?i)\b(?:machine|computer|user)(?:name)?\b\s*[:=]\s*[^\s,;]+",
        "<identity>=<redacted>",
        value,
    )
    return value


def compact_line(text, limit=700):
    return re.sub(r"\s+", " ", text).strip()[:limit]


def is_relevant_log(filename, area):
    lower = filename.lower()
    if not lower.endswith((".log", ".txt")):
        return False
    if lower.endswith(tuple(METADATA_FILES)):
        return False
    compact_area = re.sub(r"[^a-z0-9]+", "", area.lower())
    compact_path = re.sub(r"[^a-z0-9]+", "", lower)
    configured_hints = PRODUCT_LOG_HINTS.get(compact_area, ())
    area_match = (
        compact_area not in {"", "unknown", "general"}
        and (
            compact_area in compact_path
            or any(hint in lower for hint in configured_hints)
        )
    )
    global_match = any(
        marker in lower
        for marker in ("runnerlogs/", "eventviewer", "event-viewer", "crash")
    )
    return area_match or global_match


def log_relevance(filename, area):
    lower = filename.lower()
    compact_area = re.sub(r"[^a-z0-9]+", "", area.lower())
    compact_path = re.sub(r"[^a-z0-9]+", "", lower)
    configured_hints = PRODUCT_LOG_HINTS.get(compact_area, ())
    if (
        compact_area not in {"", "unknown", "general"}
        and (
            compact_area in compact_path
            or any(hint in lower for hint in configured_hints)
        )
    ):
        return 2
    return 1 if any(marker in lower for marker in ("runnerlogs/", "eventviewer", "event-viewer", "crash")) else 0


def read_entry(archive, entry):
    if entry.file_size > MAX_ENTRY_BYTES:
        raise AnalysisRejected("Selected diagnostic file exceeds the size limit")
    return decode_text(archive.read(entry))


def collect_metadata(archive, entries):
    result = []
    for entry in entries:
        name = PurePosixPath(entry.filename).name.lower()
        if entry.is_dir() or name not in METADATA_FILES:
            continue
        lines = read_entry(archive, entry).splitlines()
        if name == "windows-version.txt":
            selected = [
                line
                for line in lines
                if re.search(
                    r"product|edition|display.?version|build|architecture",
                    line,
                    re.IGNORECASE,
                )
            ]
        else:
            selected = [
                line
                for line in lines
                if re.search(
                    r"host:|architecture:|version:|microsoft\.(?:netcore|windowsdesktop)\.app",
                    line,
                    re.IGNORECASE,
                )
            ]
        text = compact_line(redact("\n".join(selected[:20])), 1000)
        if text:
            result.append((PurePosixPath(entry.filename).name, text))
    return result


def collect_signals(archive, entries, area):
    candidates = [entry for entry in entries if not entry.is_dir() and is_relevant_log(entry.filename, area)]
    candidates.sort(key=lambda entry: (log_relevance(entry.filename, area), entry.date_time), reverse=True)
    signals = []
    seen = set()
    signature_counts = {}
    total_chars = 0
    compact_area = re.sub(r"[^a-z0-9]+", "", area.lower())

    for entry in candidates[:MAX_SOURCE_FILES]:
        lines = read_entry(archive, entry).splitlines()
        for index, line in enumerate(lines):
            if not ERROR_PATTERN.search(line):
                continue
            excerpt_lines = [line]
            for following in lines[index + 1 : min(index + 4, len(lines))]:
                if re.match(r"^\s*\[(?:\d{2,4}[-/:]|\d{2}:\d{2})", following):
                    break
                excerpt_lines.append(following)
            excerpt = " ".join(excerpt_lines)
            excerpt = compact_line(redact(excerpt))
            if (
                log_relevance(entry.filename, area) == 1
                and compact_area not in {"", "unknown", "general"}
                and compact_area not in re.sub(r"[^a-z0-9]+", "", excerpt.lower())
                and not re.search(r"exception|crash|fatal|0x[0-9a-f]{6,}", excerpt, re.IGNORECASE)
            ):
                continue
            if not excerpt or excerpt in seen:
                continue
            signature = (
                tuple(sorted(re.findall(r"\b[\w.-]+\.dll\b", excerpt.lower()))),
                tuple(sorted(re.findall(r"\b0x[0-9a-f]{6,}\b", excerpt.lower()))),
            )
            if any(signature) and signature_counts.get(signature, 0) >= 2:
                continue
            seen.add(excerpt)
            if any(signature):
                signature_counts[signature] = signature_counts.get(signature, 0) + 1
            source = PurePosixPath(entry.filename).name
            rendered = f"{source}:{index + 1}: {excerpt}"
            if total_chars + len(rendered) > MAX_SIGNAL_CHARS:
                return signals
            signals.append(rendered)
            total_chars += len(rendered)
            if len(signals) >= MAX_SIGNAL_COUNT:
                return signals
    return signals


def render_context(status, *, area="Unknown", sha256="", metadata=None, signals=None, reason=""):
    metadata = metadata or []
    signals = signals or []
    lines = [
        "# Sanitized PowerToys bug report context",
        "",
        f"Status: {status}",
        f"Detected issue area: {area}",
    ]
    if sha256:
        lines.append(f"Attachment SHA-256: {sha256}")
    lines.extend(
        [
            "",
            "Raw archive contents were not provided to the model. Only the bounded, redacted diagnostics below are available.",
        ]
    )
    if reason:
        lines.extend(["", f"Safe processing result: {compact_line(reason, 300)}"])
    if metadata:
        lines.extend(["", "## Environment metadata"])
        lines.extend(f"- {name}: {value}" for name, value in metadata)
    if signals:
        lines.extend(["", "## Diagnostic signals"])
        lines.extend(f"- {signal}" for signal in signals)
    if status == "ANALYZED" and not signals:
        lines.extend(["", "No matching error or crash signatures were found in the bounded diagnostic subset."])
    return "\n".join(lines)[:MAX_CONTEXT_CHARS] + "\n"


def analyze_event(event):
    url = find_attachment_url(event)
    area = parse_issue_area(event)
    if not url:
        return render_context("NOT_FOUND", area=area)

    archive_path = None
    try:
        archive_path, sha256 = download_attachment(url)
        with zipfile.ZipFile(archive_path) as archive:
            entries = validate_archive(archive)
            metadata = collect_metadata(archive, entries)
            signals = collect_signals(archive, entries, area)
        return render_context(
            "ANALYZED",
            area=area,
            sha256=sha256,
            metadata=metadata,
            signals=signals,
        )
    except (AnalysisRejected, zipfile.BadZipFile, OSError, ValueError) as error:
        return render_context("REJECTED", area=area, reason=str(error))
    finally:
        if archive_path:
            try:
                os.unlink(archive_path)
            except FileNotFoundError:
                pass


def main():
    if len(sys.argv) != 3:
        print("Usage: bug-report-analyzer.py EVENT_JSON OUTPUT_MARKDOWN", file=sys.stderr)
        return 2
    event_path, output_path = sys.argv[1:3]
    with open(event_path, "r", encoding="utf-8") as event_file:
        event = json.load(event_file)
    context = analyze_event(event)
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    with open(output_path, "w", encoding="utf-8", newline="\n") as output_file:
        output_file.write(context)
    status_match = re.search(r"^Status: (\w+)$", context, re.MULTILINE)
    print(f"Bug report preprocessing status: {status_match.group(1) if status_match else 'UNKNOWN'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
