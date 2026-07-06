#!/usr/bin/env python3
# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""PowerScripts Python runner.

Bootstraps a user Python PowerScript in a stable, host-agnostic way. The C# host (PowerScripts.Host)
invokes this as:

    python -X utf8 _runner.py <script.py>

and writes a single JSON object to stdin describing the input. This runner:
  1. loads the user script module from <script.py>,
  2. finds its single ``powerscript_from_<input>_to_<output>`` function,
  3. calls it with arguments derived from the input JSON, and
  4. writes a single JSON result object to stdout.

Keeping this protocol identical for Windows and WSL means the user script never has to care where or
how it runs.

Input JSON fields (all optional): text, html, image_path, file_paths (list), audio_path, video_path,
params (object of name->string).

Output JSON fields (any subset): text, html, image_path, file_paths (list), audio_path, video_path.
A script may also just return a plain string (treated as text) or a path-like value.
"""

import importlib.util
import json
import re
import sys
import traceback

FUNCTION_RE = re.compile(
    r"^\s*def\s+(powerscript_from_(text|html|image|audio|video|files|none)"
    r"_to_(text|html|image|audio|video|file|files|none))\s*\(",
    re.MULTILINE,
)


def _fail(message):
    sys.stderr.write(message)
    sys.exit(1)


def _load_module(script_path):
    spec = importlib.util.spec_from_file_location("powerscript_user_module", script_path)
    if spec is None or spec.loader is None:
        _fail(f"Could not load script module from '{script_path}'.")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _find_signature(script_path):
    with open(script_path, "r", encoding="utf-8") as handle:
        source = handle.read()
    matches = FUNCTION_RE.findall(source)
    if len(matches) != 1:
        _fail(
            "Expected exactly one 'powerscript_from_<input>_to_<output>' function in the script, "
            f"found {len(matches)}."
        )
    name, input_format, _output_format = matches[0]
    return name, input_format


def _build_args(input_format, payload):
    """Maps the declared input format onto the argument passed to the user function."""
    if input_format == "text":
        return [payload.get("text", "") or ""]
    if input_format == "html":
        return [payload.get("html", "") or ""]
    if input_format == "image":
        return [payload.get("image_path", "") or ""]
    if input_format == "audio":
        return [payload.get("audio_path", "") or ""]
    if input_format == "video":
        return [payload.get("video_path", "") or ""]
    if input_format == "files":
        return [payload.get("file_paths", []) or []]
    return []  # none


def _normalize_result(result):
    """Turns a user function's return value into the output JSON contract."""
    if result is None:
        return {}
    if isinstance(result, dict):
        return result
    if isinstance(result, (list, tuple)):
        return {"file_paths": [str(item) for item in result]}
    # A bare string / path-like scalar is returned as text; callers that expect a file/image path
    # read the same "text" field and interpret it per the function's declared output format.
    return {"text": str(result)}


def main():
    if len(sys.argv) < 2:
        _fail("usage: _runner.py <script.py>")
    script_path = sys.argv[1]

    try:
        raw = sys.stdin.read()
        payload = json.loads(raw) if raw.strip() else {}
    except json.JSONDecodeError as exc:
        _fail(f"Invalid input JSON: {exc}")
        return

    try:
        function_name, input_format = _find_signature(script_path)
        module = _load_module(script_path)
        function = getattr(module, function_name, None)
        if function is None or not callable(function):
            _fail(f"Script does not define callable '{function_name}'.")
            return

        params = payload.get("params") or {}
        args = _build_args(input_format, payload)
        try:
            result = function(*args, **params) if params else function(*args)
        except TypeError:
            # The script may not accept keyword params; retry positionally only.
            result = function(*args)

        sys.stdout.write(json.dumps(_normalize_result(result)))
    except SystemExit:
        raise
    except Exception:  # noqa: BLE001 - surface any script error to the host's stderr
        _fail("PowerScript raised an exception:\n" + traceback.format_exc())


if __name__ == "__main__":
    main()
