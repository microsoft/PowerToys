# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""
Advanced Paste – Script Runner (V3 Named Function Interface)

This runner is shipped with PowerToys and is NOT user-editable.
It loads a user script, discovers the single advanced_paste_from_<input>_to_<output>
function by name convention, calls it with the current clipboard data, and formats
the return value into JSON on stdout.

Each script must define exactly one function matching the pattern:
  def advanced_paste_from_<input>_to_<output>(<param>)

Supported input types:
  - text, html, image, files

Required output types (declared via _to_ suffix):
  - text, html, image, file, files

Examples:
  - advanced_paste_from_text_to_text(text: str)    → output is text
  - advanced_paste_from_text_to_image(text: str)   → output is image
  - advanced_paste_from_image_to_text(image_path)  → output is text
  - advanced_paste_from_files_to_text(file_paths)  → output is text

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


def _apply_output_hint(result, hint: str) -> dict:
    """
    Force the output to the type specified by the _to_ suffix in the function name.
    Converts the return value to match the hinted type.
    """
    if result is None:
        if hint == "text":
            return {"result_type": "text", "text": ""}
        elif hint == "html":
            return {"result_type": "html", "html": ""}
        elif hint == "image":
            return {"result_type": "image", "image_path": ""}
        elif hint in ("file", "files"):
            return {"result_type": hint, "file_paths": []}

    if hint == "text":
        return {"result_type": "text", "text": str(result) if not isinstance(result, str) else result}
    elif hint == "html":
        return {"result_type": "html", "html": str(result) if not isinstance(result, str) else result}
    elif hint == "image":
        path = str(result)
        return {"result_type": "image", "image_path": path}
    elif hint == "file":
        if isinstance(result, (list, tuple)):
            paths = [str(p) for p in result]
        else:
            paths = [str(result)]
        return {"result_type": "file", "file_paths": paths}
    elif hint == "files":
        if isinstance(result, (list, tuple)):
            paths = [str(p) for p in result]
        else:
            paths = [str(result)]
        return {"result_type": "files", "file_paths": paths}

    # Fallback (shouldn't happen with valid hints)
    return {"result_type": "text", "text": str(result)}

# Pattern matching advanced_paste_from_<input>_to_<output> function names.
_AP_FUNCTION_PATTERN = re.compile(
    r"^advanced_paste_from_(text|html|image|files)_to_(text|html|image|file|files)$"
)


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


def _discover_ap_function(module) -> tuple:
    """
    Discover the single advanced_paste_from_<input>_to_<output> function in the module.
    Returns a tuple (input_type, output_type, function) or None if not found.
    Exits with error if multiple functions are defined.
    """
    matches = []
    for name in dir(module):
        match = _AP_FUNCTION_PATTERN.match(name)
        if match:
            fn = getattr(module, name)
            if callable(fn):
                input_type = match.group(1)
                output_type = match.group(2)
                matches.append((input_type, output_type, fn))

    if len(matches) == 0:
        return None
    if len(matches) > 1:
        names = [f"advanced_paste_from_{m[0]}_to_{m[1]}" for m in matches]
        print(
            f"Error: script defines multiple advanced_paste_from_*_to_* functions "
            f"({', '.join(names)}). Only one is allowed per script.",
            file=sys.stderr,
        )
        sys.exit(1)
    return matches[0]


def _format_output(result, output_type: str) -> dict:
    """
    Format the return value according to the declared output type from the function name.
    The output_type comes from the _to_ suffix and is always provided.
    """
    if result is None:
        if output_type in ("file", "files"):
            return {"result_type": output_type, "file_paths": []}
        elif output_type == "image":
            return {"result_type": "image", "image_path": ""}
        elif output_type == "html":
            return {"result_type": "html", "html": ""}
        return {"result_type": "text", "text": ""}

    return _apply_output_hint(result, output_type)


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

    # Discover the single advanced_paste_from_* function.
    ap_result = _discover_ap_function(module)

    if ap_result is None:
        print(
            f"Error: script '{os.path.basename(script_path)}' does not define an "
            f"advanced_paste_from_<input>_to_<output> function.\n"
            f"Example: def advanced_paste_from_text_to_text(text): return text.upper()",
            file=sys.stderr,
        )
        sys.exit(1)

    input_type, output_type, fn = ap_result

    # Determine the input data key for this function's input type.
    input_map = {
        "text": "text",
        "html": "html",
        "image": "image_path",
        "files": "file_paths",
    }

    key = input_map.get(input_type, input_type)
    input_value = data.get(key)

    # Expose work_dir as environment variable so scripts can write output files
    # to a location accessible from both WSL and Windows (under /mnt/c/...).
    work_dir = data.get("work_dir", "")
    if work_dir:
        os.environ["ADVANCED_PASTE_WORK_DIR"] = work_dir

    # Check if the clipboard has matching data for this script's input type.
    formats = data.get("format", ["text"])
    if isinstance(formats, str):
        formats = [formats]

    if input_type not in formats:
        print(
            f"Error: script expects '{input_type}' input but clipboard has [{', '.join(formats)}].",
            file=sys.stderr,
        )
        sys.exit(1)

    if not input_value:
        print(
            f"Error: no data available for format '{input_type}' "
            f"(expected '{key}' in input payload).",
            file=sys.stderr,
        )
        sys.exit(1)

    # Call the function.
    result = fn(input_value)
    output = _format_output(result, output_type)

    # Output JSON result.
    json.dump(output, sys.stdout, ensure_ascii=False)


if __name__ == "__main__":
    main()
