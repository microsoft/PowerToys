# Advanced Paste – Python Scripts

Advanced Paste supports user-defined Python scripts that transform clipboard content. Scripts are
discovered automatically from a configurable folder and appear as actions in the Advanced Paste UI.

## Quick start

1. Open the scripts folder — by default `%LOCALAPPDATA%\Microsoft\PowerToys\AdvancedPaste\Scripts`.
   You can change this in **Settings → Advanced Paste → Python scripts → Scripts folder**.
2. Drop a `.py` file into the folder.
3. Define one `advanced_paste_from_<input>_to_<output>` function (see [Writing a script](#writing-a-script)).
4. Open the Advanced Paste UI (`Win+Shift+V`) — your script will appear in the action list.

> **Important:** Each `.py` file must define exactly one `advanced_paste_from_<input>_to_<output>`
> function. Scripts with zero or multiple such functions are ignored.

## Writing a script

You write a single Python function whose **name** declares both what clipboard input it accepts
and what output type it produces.
No imports from PowerToys are needed — zero setup, zero dependencies on our side.

### Function naming convention

The function name follows the pattern:

```
advanced_paste_from_<input>_to_<output>(<param>)
```

**Input types** (what the function receives):

| Input | Parameter | When it runs |
|-------|-----------|--------------|
| `text` | `str` — clipboard text | Clipboard has text |
| `html` | `str` — clipboard HTML | Clipboard has HTML |
| `image` | `str` — path to temp image file | Clipboard has an image |
| `audio` | `str` — path to audio file | Clipboard has an audio file |
| `video` | `str` — path to video file | Clipboard has a video file |
| `files` | `list[str]` — file paths | Clipboard has files |

**Output types** (what the function produces — declared via `_to_` suffix):

| Output | Effect |
|--------|--------|
| `text` | Sets clipboard to text |
| `html` | Sets clipboard to HTML |
| `image` | Sets clipboard to image |
| `audio` | Sets clipboard to audio file |
| `video` | Sets clipboard to video file |
| `file` | Sets clipboard to a file |
| `files` | Sets clipboard to multiple files |

### Return value

The return value is interpreted according to the declared output type:

| Output type | Expected return value |
|-------------|---------------------|
| `text` | `str` (or any value — will be converted via `str()`) |
| `html` | `str` containing HTML |
| `image` | `str` or `pathlib.Path` pointing to an image file |
| `file` | `str` or `pathlib.Path` pointing to a file |
| `files` | `list` of `str`/`pathlib.Path` file paths |

Returning `None` produces an empty result (no-op).

## Examples

### Minimal — uppercase text

```python
def advanced_paste_from_text_to_text(text):
    return text.upper()
```

That's it. No headers required, no imports from PowerToys.

### With optional metadata

```python
# @advancedpaste:name   Reverse Text
# @advancedpaste:desc   Reverses clipboard text character by character

def advanced_paste_from_text_to_text(text):
    return text[::-1]
```

### Text to HTML

```python
# @advancedpaste:name   Markdown Table to HTML
# @advancedpaste:desc   Convert a markdown table to an HTML table

def advanced_paste_from_text_to_html(text):
    headers = text.splitlines()[0].split("|")
    return f"<table><tr>{''.join(f'<th>{h.strip()}</th>' for h in headers if h.strip())}</tr></table>"
```

### Image to text (OCR)

```python
# @advancedpaste:requires pytesseract

def advanced_paste_from_image_to_text(image_path):
    import pytesseract
    return pytesseract.image_to_string(image_path).strip()
```

### Save text as file

```python
import os
from pathlib import Path
import tempfile

def advanced_paste_from_text_to_file(text):
    # Use ADVANCED_PASTE_WORK_DIR for WSL compatibility; falls back to temp dir on Windows.
    out_dir = os.environ.get("ADVANCED_PASTE_WORK_DIR") or tempfile.gettempdir()
    out = Path(out_dir) / "clipboard.txt"
    out.write_text(text, encoding="utf-8")
    return out
```

### Image processing (image → image)

```python
import os
from PIL import Image
from pathlib import Path
import tempfile

def advanced_paste_from_image_to_image(image_path):
    """Convert image to grayscale."""
    img = Image.open(image_path).convert("L")
    out_dir = os.environ.get("ADVANCED_PASTE_WORK_DIR") or tempfile.gettempdir()
    out = Path(out_dir) / "gray.png"
    img.save(out)
    return out
```

### File listing (files → text)

```python
import os

def advanced_paste_from_files_to_text(file_paths):
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
| Python interpreter | Path to the Python executable (Windows mode). Leave blank for auto-detection. | *(auto-detect)* |
| Use WSL | Run scripts in Windows Subsystem for Linux instead of native Windows Python. | Off |
| WSL distribution | Which WSL distro to use (e.g. `Ubuntu`). Leave blank for the default distribution. | *(default)* |
| Scripts folder | Folder to scan for `.py` scripts. | `%LOCALAPPDATA%\Microsoft\PowerToys\AdvancedPaste\Scripts` |

### WSL mode

When **Use WSL** is enabled:

- Scripts are executed via `wsl.exe bash -l -c "python3 ..."` using the configured distribution.
- The scripts folder remains on the Windows filesystem; paths are automatically translated
  to `/mnt/c/...` format for WSL access.
- Package installation uses `pip3 install` inside the WSL environment.
- Output files from scripts must be written under `/mnt/` (the Windows-mounted filesystem)
  so they can be accessed from Windows. The runner sets the `ADVANCED_PASTE_WORK_DIR` environment
  variable to a temp directory under `/mnt/c/...` — use it instead of `tempfile.gettempdir()`
  when producing file output for cross-platform compatibility.

> **Tip:** If you have Python installed only in WSL (not on Windows), enable WSL mode
> to use your existing WSL Python environment with all its packages.

## Tips

- Each `.py` file must contain exactly one `advanced_paste_from_<input>_to_<output>` function.
  If you need to handle multiple input types, create separate script files for each.
- A `.py` file without any matching function is ignored — use this for helper modules
  that other scripts can import.
- Scripts can be tested from the command line:
  ```
  echo {"format":["text"],"text":"hello"} | python _runner.py my_script.py
  ```
- The script's directory is added to `sys.path` at runtime, so you can import sibling `.py`
  files as helper modules.
