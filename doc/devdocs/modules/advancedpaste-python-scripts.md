# Advanced Paste – Python Scripts

Advanced Paste supports user-defined Python scripts that transform clipboard content. Scripts are
discovered automatically from a configurable folder and appear as actions in the Advanced Paste UI.

## Quick start

1. Open the scripts folder — by default `%LOCALAPPDATA%\Microsoft\PowerToys\AdvancedPaste\Scripts`.
   You can change this in **Settings → Advanced Paste → Python scripts → Scripts folder**.
2. Drop a `.py` file into the folder.
3. Define one or more `ap_from_*` functions (see [Writing a script](#writing-a-script)).
4. Open the Advanced Paste UI (`Win+Shift+V`) — your script will appear in the action list.

> **Important:** Only `.py` files that define at least one `ap_from_*` function are loaded.
> Plain scripts without these functions are ignored.

## Writing a script

You write normal Python functions whose **names** declare what clipboard input they accept.
No imports from PowerToys are needed — zero setup, zero dependencies on our side.

### Function naming convention

| Function name | Input parameter | When it runs |
|---------------|-----------------|--------------|
| `ap_from_text(text)` | `str` — clipboard text | Clipboard has text |
| `ap_from_html(html)` | `str` — clipboard HTML | Clipboard has HTML |
| `ap_from_image(image_path)` | `str` — path to temp image file | Clipboard has an image |
| `ap_from_files(file_paths)` | `list[str]` — file paths | Clipboard has files |

A single script can define multiple functions to handle different input types.

### Return value convention

The return value determines what gets placed on the clipboard:

| Return type | Effect |
|-------------|--------|
| `str` | Sets clipboard to text |
| `pathlib.Path` (`.png`, `.jpg`, etc.) | Sets clipboard to image |
| `pathlib.Path` (other extension) | Sets clipboard to file |
| `list` of `Path`/`str` | Sets clipboard to multiple files |
| `dict` with `"type"` key | Explicit output type (escape hatch — see below) |
| `None` | No-op (clipboard unchanged) |

### Dict escape hatch

For cases where the return type can't be inferred from the value alone:

```python
def ap_from_text(text):
    html = f"<b>{text.upper()}</b>"
    return {"type": "html", "value": html}
```

Supported `"type"` values: `"text"`, `"html"`, `"image"`, `"file"`, `"files"`.

## Examples

### Minimal — uppercase text

```python
def ap_from_text(text):
    return text.upper()
```

That's it. No headers required, no imports from PowerToys.

### With optional metadata

```python
# @advancedpaste:name   Reverse Text
# @advancedpaste:desc   Reverses clipboard text character by character

def ap_from_text(text):
    return text[::-1]
```

### Image processing

```python
from PIL import Image
from pathlib import Path
import tempfile

def ap_from_image(image_path):
    """Convert image to grayscale."""
    img = Image.open(image_path).convert("L")
    out = Path(tempfile.gettempdir()) / "gray.png"
    img.save(out)
    return out
```

### Return HTML

```python
def ap_from_text(text):
    return {"type": "html", "value": f"<pre><code>{text}</code></pre>"}
```

### Multiple input types in one script

```python
def ap_from_text(text):
    return f"Text ({len(text)} chars): {text[:100]}"

def ap_from_files(file_paths):
    return "\n".join(file_paths)
```

### File listing

```python
import os

def ap_from_files(file_paths):
    lines = []
    for p in file_paths:
        size = os.path.getsize(p)
        lines.append(f"{os.path.basename(p)} ({size} bytes)")
    return "\n".join(lines)
```

## Header tags

All header tags are **optional**. Tags are placed in comment lines at the top of the script.

| Tag | Description |
|-----|-------------|
| `name` | Display name in the Advanced Paste UI. If omitted, the filename is used. |
| `desc` | Short description / tooltip. |
| `disabled` | Presence of this tag disables the script (it won't appear in the UI). |
| `requires` | Declare Python package dependencies (see [Dependencies](#declaring-dependencies)). |

### Example header

```python
# @advancedpaste:name   My Formatter
# @advancedpaste:desc   Formats clipboard text as markdown table
```

To disable a script without deleting it, add:

```python
# @advancedpaste:disabled
```

Remove the line to re-enable.

## Declaring dependencies

Use `requires` to declare Python packages the script needs:

```python
# @advancedpaste:requires PIL=Pillow
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

- A `.py` file without any `ap_from_*` function is ignored — use this for helper modules
  that other scripts can import.
- Scripts can be tested from the command line:
  ```
  echo {"format":"text","text":"hello"} | python _runner.py my_script.py
  ```
- The script's directory is added to `sys.path` at runtime, so you can import sibling `.py`
  files as helper modules.
