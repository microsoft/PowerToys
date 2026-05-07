# Advanced Paste – Python Scripts

Advanced Paste supports user-defined Python scripts that transform clipboard content. Scripts are
discovered automatically from a configurable folder and appear as actions in the Advanced Paste UI.

## Quick start

1. Open the scripts folder — by default `%LOCALAPPDATA%\Microsoft\PowerToys\AdvancedPaste\Scripts`.
   You can change this in **Settings → Advanced Paste → Python scripts → Scripts folder**.
2. Drop a `.py` file into the folder.
3. Define a `convert()` function (see [V2 Interface](#v2-interface-recommended)).
4. Open the Advanced Paste UI (`Win+Shift+V`) — your script will appear in the action list.

## V2 Interface (Recommended)

The V2 interface is the simplest way to write scripts. You define a single `convert()` function
that receives clipboard data as arguments and returns the result. **No platform-specific code needed.**

### Minimal example — reverse text:

```python
# @advancedpaste:name  Reverse Text

def convert(text):
    """Reverse the clipboard text."""
    return text[::-1]
```

That's it. No `import sys`, no `json.load`, no clipboard libraries. Advanced Paste handles all the plumbing.

### How it works

1. Advanced Paste reads the clipboard and serializes it as JSON.
2. A built-in runner inspects your `convert()` function signature.
3. Only the parameters your function declares are passed as keyword arguments.
4. Your function returns the result — Advanced Paste sets it on the clipboard and pastes.

### Parameter convention

Declare only the parameters you need:

| Parameter | Type | Content |
|-----------|------|---------|
| `text` | `str` | Clipboard text content |
| `html` | `str` | Clipboard HTML content |
| `image_path` | `str` | Path to temp PNG file of clipboard image |
| `image` | `str` | Alias for `image_path` |
| `file_paths` | `list[str]` | List of clipboard file paths |
| `files` | `list[str]` | Alias for `file_paths` |
| `file_path` | `str` | First file path (convenience for single-file) |
| `work_dir` | `str` | Writable temp directory (cleaned up after execution) |
| `format` | `list[str]` | Detected clipboard format names |

**Format inference:** Advanced Paste infers which clipboard formats your script supports from
the parameter names. A script with `def convert(text)` only appears when the clipboard has text.
Use `**kwargs` to accept all formats.

### Return value convention

| Return type | Effect |
|-------------|--------|
| `str` | Sets clipboard to text |
| `dict` | Full control — must include `result_type` key (see [Output schema](#output-payload)) |
| `pathlib.Path` | Sets clipboard to that file |
| `list` of paths | Sets clipboard to multiple files |
| `None` | No-op (clipboard unchanged) |

### More examples

**Convert text to uppercase:**
```python
# @advancedpaste:name  Upper Case

def convert(text):
    return text.upper()
```

**Convert image to grayscale:**
```python
# @advancedpaste:name  Grayscale Image
# @advancedpaste:requires PIL=Pillow

from PIL import Image
from pathlib import Path

def convert(image_path, work_dir):
    img = Image.open(image_path).convert("L")
    out = Path(work_dir) / "gray.png"
    img.save(out)
    return out
```

**Return HTML:**
```python
# @advancedpaste:name  Wrap in Code Block

def convert(text):
    return {
        "result_type": "html",
        "html": f"<pre><code>{text}</code></pre>",
        "text": text,  # fallback for apps that don't support HTML
    }
```

**Accept any format with kwargs:**
```python
# @advancedpaste:name  Debug Clipboard

def convert(**kwargs):
    """Show what's on the clipboard as formatted text."""
    import json
    return json.dumps(kwargs, indent=2, default=str)
```

## Header format

The only required header is `name`:

```python
# @advancedpaste:name   My Script Name
```

### Optional tags

| Tag | Description |
|-----|-------------|
| `name` | **Required.** Display name shown in the Advanced Paste UI. |
| `desc` | Short description / tooltip. (Can also use the `convert()` docstring.) |
| `formats` | Override auto-detected formats. Comma-separated: `text`, `html`, `image`, `file`, `any`. |
| `requires` | Declare Python package dependencies (see [Declaring dependencies](#declaring-dependencies)). |
| `enabled` | Set to `false` to disable the script without deleting it. |

### Tags no longer needed in V2

| Tag | Why |
|-----|-----|
| `platform` | Eliminated — V2 scripts run identically on Windows and WSL. |
| `version` | Reserved, not useful in practice. |

## Legacy Interface (V1)

Scripts that do NOT define a `convert()` function are treated as V1 (legacy) scripts and
continue to work as before:

### Windows mode (`platform windows`)

The script runs directly and owns the clipboard via `win32clipboard`.

```python
# @advancedpaste:name   Reverse text
# @advancedpaste:formats text
# @advancedpaste:platform windows
import win32clipboard

win32clipboard.OpenClipboard()
text = win32clipboard.GetClipboardData(win32clipboard.CF_UNICODETEXT)
win32clipboard.EmptyClipboard()
win32clipboard.SetClipboardData(win32clipboard.CF_UNICODETEXT, text[::-1])
win32clipboard.CloseClipboard()
```

### WSL / Linux mode (`platform linux`)

The script reads JSON from stdin and writes JSON to stdout.

```python
# @advancedpaste:name   WSL Upper Case
# @advancedpaste:formats text
# @advancedpaste:platform linux
import sys, json

data = json.load(sys.stdin)
text = data.get("text", "")
json.dump({"result_type": "text", "text": text.upper()}, sys.stdout)
```

## Input/Output JSON Schema (for V1 Linux and advanced V2 dict returns)

### Input payload

```jsonc
{
  "version": 2,
  "format": ["text"],           // array of detected clipboard format names
  "work_dir": "C:\\Temp\\...",  // writable temp directory
  "text": "Hello, world!",     // present when clipboard has text
  "html": "<b>Hello</b>",      // present when clipboard has HTML
  "image_path": "C:\\...\\input.png",  // present when clipboard has an image
  "file_paths": ["C:\\...\\file.txt"]  // present when clipboard has files
}
```

### Output payload

```jsonc
{
  "result_type": "text",        // "text" | "html" | "image" | "file" | "files"
  "text": "HELLO, WORLD!",     // for result_type "text"
  "html": "<b>HELLO</b>",      // for result_type "html"
  "image_path": "C:\\...\\output.png",  // for result_type "image"
  "file_paths": ["C:\\...\\out.txt"]    // for result_type "file"/"files"
}
```

## Declaring dependencies

Use `requires` to declare Python packages the script needs:

```python
# @advancedpaste:requires markitdown='markitdown[all]'
# @advancedpaste:requires cv2=opencv-python-headless numpy requests
```

Each token is either:

- **`import_name`** — the pip package is assumed to have the same name (e.g. `requests`).
- **`import_name=pip_package`** — when the import name differs from the pip package
  (e.g. `cv2=opencv-python-headless`, `PIL=Pillow`).

### Automatic import detection

Advanced Paste also scans the script body for `import` and `from ... import` statements
and cross-references them against the Python standard library. Any non-stdlib import
that is not already installed triggers a prompt to install it automatically.

## Security — script trust

The first time a script is executed (or after it has been modified), Advanced Paste
shows a confirmation dialog. Upon approval the SHA-256 hash of the script is stored.
Subsequent runs of the unchanged file skip the dialog.

## Error handling

When a script fails, Advanced Paste extracts the Python traceback from stderr and
displays a user-friendly summary in the UI:

- **ModuleNotFoundError** — identifies the missing module and suggests installing it.
- **SyntaxError** — shows the file and line number.
- **Timeout** — shows the configured timeout value (default 30 s; configurable in Settings).
- **Other errors** — shows the last line of the traceback as a summary, with the full
  traceback available in the expandable *Details* section.

## Settings

The following settings are available under **Settings → Advanced Paste → Python scripts**:

| Setting | Description | Default |
|---------|-------------|---------|
| Python interpreter | Path to the Python executable. Leave blank for auto-detection. | *(auto-detect)* |
| Scripts folder | Folder to scan for `.py` scripts. | `%LOCALAPPDATA%\Microsoft\PowerToys\AdvancedPaste\Scripts` |

## Tips

- Put reusable helper functions in a separate `.py` file without a `# @advancedpaste:name`
  header — it will be ignored by the script discovery and can be imported by other scripts.
- The `work_dir` parameter points to a temporary directory that is cleaned up after execution.
  Use it for intermediate files (e.g., image processing output).
- V2 scripts are testable from the command line:
  ```
  echo {"text":"hello"} | python _runner.py my_script.py
  ```
