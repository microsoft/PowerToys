# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""
Advanced Paste – Script Runner (V2 Interface)

This runner is shipped with PowerToys and is NOT user-editable.
It loads a user script, inspects its convert() function signature,
passes the relevant clipboard data as keyword arguments, and returns
the result as JSON on stdout.

Protocol:
  - Input:  JSON on stdin (clipboard data from C#)
  - Output: JSON on stdout (result for C# to set on clipboard)
  - Errors: stderr (displayed to user on failure)

Input JSON schema:
  {
    "version": 2,
    "format": ["text"],
    "work_dir": "/tmp/...",
    "text": "...",
    "html": "...",
    "image_path": "...",
    "file_paths": ["..."]
  }

Output JSON schema:
  {
    "result_type": "text" | "html" | "image" | "file" | "files",
    "text": "...",
    "html": "...",
    "image_path": "...",
    "file_paths": ["..."]
  }
"""

import importlib.util
import inspect
import json
import os
import sys
from pathlib import Path


def _load_user_module(script_path: str):
    """Dynamically load the user script as a Python module."""
    spec = importlib.util.spec_from_file_location("_user_script", script_path)
    if spec is None or spec.loader is None:
        raise ImportError(f"Cannot load script: {script_path}")
    module = importlib.util.module_from_spec(spec)
    # Add the script's directory to sys.path so relative imports/helpers work.
    script_dir = os.path.dirname(os.path.abspath(script_path))
    if script_dir not in sys.path:
        sys.path.insert(0, script_dir)
    spec.loader.exec_module(module)
    return module


def _build_kwargs(sig: inspect.Signature, data: dict) -> dict:
    """
    Build keyword arguments for the convert() function based on its signature.

    Only passes parameters that the function actually declares, so that a simple
    script like `def convert(text): ...` only receives the text field.
    """
    # Map from parameter name to the corresponding key in the input JSON.
    param_to_json_key = {
        "text": "text",
        "html": "html",
        "image_path": "image_path",
        "image": "image_path",      # alias
        "file_paths": "file_paths",
        "files": "file_paths",      # alias
        "file_path": "file_paths",  # alias (will be first element)
        "work_dir": "work_dir",
        "format": "format",
        "formats": "format",        # alias
    }

    kwargs = {}
    for param_name, param in sig.parameters.items():
        if param_name == "self":
            continue

        # If the function accepts **kwargs, pass everything available.
        if param.kind == inspect.Parameter.VAR_KEYWORD:
            for key, value in data.items():
                if key not in kwargs and key != "version":
                    kwargs[key] = value
            break

        json_key = param_to_json_key.get(param_name, param_name)
        if json_key in data:
            value = data[json_key]
            # Special case: file_path (singular) gets the first element of file_paths list.
            if param_name == "file_path" and isinstance(value, list):
                value = value[0] if value else None
            kwargs[param_name] = value
        elif param.default is not inspect.Parameter.empty:
            # Has a default value; don't pass it — let Python use the default.
            pass
        else:
            # Required parameter not available in input — pass None.
            kwargs[param_name] = None

    return kwargs


def _normalize_result(result) -> dict:
    """
    Normalize the return value of convert() into the output JSON schema.

    Accepted return types:
      - str              → {"result_type": "text", "text": "..."}
      - dict             → passed through (must include "result_type")
      - pathlib.Path     → {"result_type": "file", "file_paths": ["..."]}
      - list of str/Path → {"result_type": "files", "file_paths": [...]}
      - None             → {"result_type": "text", "text": ""}
    """
    if result is None:
        return {"result_type": "text", "text": ""}

    if isinstance(result, str):
        return {"result_type": "text", "text": result}

    if isinstance(result, Path):
        return {"result_type": "file", "file_paths": [str(result)]}

    if isinstance(result, dict):
        if "result_type" not in result:
            # Infer result_type from keys present (most specific first).
            if "html" in result:
                result["result_type"] = "html"
            elif "image_path" in result:
                result["result_type"] = "image"
            elif "file_paths" in result:
                result["result_type"] = "files"
            elif "text" in result:
                result["result_type"] = "text"
            else:
                result["result_type"] = "text"
                result["text"] = ""
        return result

    if isinstance(result, (list, tuple)):
        paths = [str(p) for p in result]
        return {"result_type": "files", "file_paths": paths}

    # Fallback: convert to string.
    return {"result_type": "text", "text": str(result)}


def main():
    if len(sys.argv) < 2:
        print("Usage: _runner.py <script_path>", file=sys.stderr)
        sys.exit(1)

    script_path = sys.argv[1]

    if not os.path.isfile(script_path):
        print(f"Error: script not found: {script_path}", file=sys.stderr)
        sys.exit(1)

    # Read input payload from stdin.
    try:
        data = json.load(sys.stdin)
    except json.JSONDecodeError as e:
        print(f"Error: invalid JSON input: {e}", file=sys.stderr)
        sys.exit(1)

    # Load the user script.
    module = _load_user_module(script_path)

    # Find the convert() function.
    convert_fn = getattr(module, "convert", None)
    if convert_fn is None or not callable(convert_fn):
        print(
            f"Error: script '{os.path.basename(script_path)}' does not define a 'convert()' function.\n"
            f"Please add: def convert(text=None, **kwargs): ...",
            file=sys.stderr,
        )
        sys.exit(1)

    # Inspect signature and build keyword arguments.
    sig = inspect.signature(convert_fn)
    kwargs = _build_kwargs(sig, data)

    # Call the user function.
    result = convert_fn(**kwargs)

    # Normalize and output.
    output = _normalize_result(result)
    json.dump(output, sys.stdout, ensure_ascii=False)


if __name__ == "__main__":
    main()
