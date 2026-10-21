#!/usr/bin/env python3
# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""PowerScripts Python runner.

Bootstraps a user Python PowerScript in a stable, host-agnostic way. The C# host (PowerScripts.Host)
invokes this as:

    python -X utf8 _runner.py <script.py>

and writes a single JSON object to stdin describing the input. This runner:
  1. validates the descriptor-declared entry and input format,
  2. loads the user script module from <script.py> and resolves that function,
  3. calls it with arguments derived from the input JSON, and
  4. writes a single JSON result object to stdout.

Keeping this protocol identical for Windows and WSL means the user script never has to care where or
how it runs.

Input JSON fields (all optional): text, file_paths (list), params (object of name->string).
Inline HTML uses text; images, audio, video, and HTML documents use file_paths.
The host supplies ``entry`` (the descriptor's function name)
and ``input_format`` (the resolved input shape).

Output JSON is {}, {"text": "..."}, or {"file_paths": ["..."]}, matching the descriptor.
A plain string is text, never an inferred path. Return a list for files, even one HTML/media file.
"""

import importlib.util
import inspect
import json
import os
import sys
import traceback

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


def _build_args(input_format, payload):
    """Maps the declared input format onto the argument passed to the user function."""
    if input_format == "text":
        return [payload.get("text", "") or ""]
    if input_format == "files":
        return [payload.get("file_paths", []) or []]
    if input_format == "none":
        return []
    _fail(f"Unsupported input format '{input_format}'.")


def _normalize_result(result):
    """Turns a user function's return value into the output JSON contract."""
    if result is None:
        return {}
    if isinstance(result, dict):
        return result
    if isinstance(result, (list, tuple)):
        return {"file_paths": [os.fspath(item) for item in result]}
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
        entry = payload.get("entry")
        if not isinstance(entry, str) or not entry.strip():
            _fail("Missing descriptor entry function ('x-powerscript.function').")

        args = _build_args(payload.get("input_format"), payload)
        module = _load_module(script_path)

        function = getattr(module, entry, None)
        if function is None or not callable(function):
            _fail(f"Script does not define callable '{entry}'.")
            return

        params = payload.get("params") or {}

        # Decide up front whether the function accepts the keyword params, rather than catching a
        # TypeError from the call itself: a TypeError raised *inside* the function body (after it has
        # already run side effects) would otherwise trigger a positional retry and double-execute it.
        if params:
            try:
                inspect.signature(function).bind(*args, **params)
            except TypeError:
                params = {}

        result = function(*args, **params) if params else function(*args)

        sys.stdout.write(json.dumps(_normalize_result(result)))
    except SystemExit:
        raise
    except Exception:  # noqa: BLE001 - surface any script error to the host's stderr
        _fail("PowerScript raised an exception:\n" + traceback.format_exc())


if __name__ == "__main__":
    main()
