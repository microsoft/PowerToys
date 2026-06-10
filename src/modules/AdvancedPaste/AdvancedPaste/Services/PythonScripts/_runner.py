# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""
Advanced Paste – Script Runner (V3 Named Function Interface)

This runner is shipped with PowerToys and is NOT user-editable.
It loads a user script, discovers ap_from_* functions by name convention,
calls the one matching the current clipboard format, and normalizes the
return value into JSON on stdout.

Supported function names (declare what clipboard input the function handles):
  - advanced_paste_from_text(text: str)
  - advanced_paste_from_html(html: str)
  - advanced_paste_from_image(image_path: str)
  - advanced_paste_from_files(file_paths: list)

Output is inferred from the return value:
  - str              → text (or html if starts with '<' and contains html tags)
  - pathlib.Path     → image (if .png/.jpg/.jpeg/.bmp/.gif/.webp) or file
  - list of paths    → files
  - dict             → explicit: {"type": "text|html|image|file|files", "value": "..."}
  - None             → no-op (empty text)

Protocol:
  - Input:  JSON on stdin (clipboard data from C#)
  - Output: JSON on stdout (result for C# to set on clipboard)
  - Errors: stderr (displayed to user on failure)
"""

import importlib.util
import json
import os
import re
import sys
from pathlib import Path

# Image file extensions recognized as image output.
_IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tiff", ".ico"}

# Pattern matching advanced_paste_from_* function names.
_AP_FUNCTION_PATTERN = re.compile(r"^advanced_paste_from_(text|html|image|files)$")


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


def _discover_ap_functions(module) -> dict:
    """
    Discover all advanced_paste_from_* functions in the module.
    Returns a dict mapping input_type → function.
    """
    functions = {}
    for name in dir(module):
        match = _AP_FUNCTION_PATTERN.match(name)
        if match:
            fn = getattr(module, name)
            if callable(fn):
                input_type = match.group(1)
                functions[input_type] = fn
    return functions


def _infer_output(result) -> dict:
    """
    Infer the output type from the return value.

    Rules:
      - None             → empty text
      - str              → text
      - Path(.png/etc)   → image
      - Path(other)      → file
      - list/tuple       → files
      - dict with "type" → explicit (escape hatch)
      - dict without     → infer from keys
    """
    if result is None:
        return {"result_type": "text", "text": ""}

    # Explicit dict escape hatch: {"type": "html", "value": "<b>hi</b>"}
    if isinstance(result, dict):
        if "type" in result:
            rtype = result["type"]
            value = result.get("value", "")
            if rtype == "text":
                return {"result_type": "text", "text": str(value)}
            elif rtype == "html":
                return {"result_type": "html", "html": str(value)}
            elif rtype == "image":
                return {"result_type": "image", "image_path": str(value)}
            elif rtype == "file":
                return {"result_type": "file", "file_paths": [str(value)]}
            elif rtype == "files":
                paths = [str(p) for p in value] if isinstance(value, (list, tuple)) else [str(value)]
                return {"result_type": "files", "file_paths": paths}
        # Dict without explicit "type" key: infer from known keys
        if "html" in result:
            return {"result_type": "html", **result}
        if "image_path" in result:
            return {"result_type": "image", **result}
        if "file_paths" in result:
            return {"result_type": "files", **result}
        if "text" in result:
            return {"result_type": "text", **result}
        return {"result_type": "text", "text": str(result)}

    # String → text
    if isinstance(result, str):
        return {"result_type": "text", "text": result}

    # Path → image or file based on extension
    if isinstance(result, Path):
        ext = result.suffix.lower()
        if ext in _IMAGE_EXTENSIONS:
            return {"result_type": "image", "image_path": str(result)}
        return {"result_type": "file", "file_paths": [str(result)]}

    # List/tuple → files
    if isinstance(result, (list, tuple)):
        paths = [str(p) for p in result]
        return {"result_type": "files", "file_paths": paths}

    # Fallback: convert to string
    return {"result_type": "text", "text": str(result)}


# ---------------------------------------------------------------------------
# Main entry point
# ---------------------------------------------------------------------------

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

    # Discover advanced_paste_from_* functions.
    ap_functions = _discover_ap_functions(module)

    if not ap_functions:
        print(
            f"Error: script '{os.path.basename(script_path)}' does not define any "
            f"advanced_paste_from_* functions.\n"
            f"Please add: def advanced_paste_from_text(text): return text.upper()",
            file=sys.stderr,
        )
        sys.exit(1)

    # Determine current clipboard format.
    current_format = data.get("format", "text")
    if isinstance(current_format, list):
        current_format = current_format[0] if current_format else "text"

    # Find the matching function.
    fn = ap_functions.get(current_format)
    if fn is None:
        available = ", ".join(f"advanced_paste_from_{k}" for k in sorted(ap_functions.keys()))
        print(
            f"Error: no function for clipboard format '{current_format}'.\n"
            f"Available functions: {available}",
            file=sys.stderr,
        )
        sys.exit(1)

    # Get the input value.
    input_map = {
        "text": "text",
        "html": "html",
        "image": "image_path",
        "files": "file_paths",
    }
    input_key = input_map.get(current_format, "text")
    input_value = data.get(input_key, "")

    # Call the function.
    result = fn(input_value)
    output = _infer_output(result)

    # Output JSON result.
    json.dump(output, sys.stdout, ensure_ascii=False)


if __name__ == "__main__":
    main()
