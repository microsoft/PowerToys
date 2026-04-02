# Advanced Paste – Python Scripts

Advanced Paste supports user-defined Python scripts that transform clipboard content. Scripts are
discovered automatically from a configurable folder and appear as actions in the Advanced Paste UI.

## Quick start

1. Open the scripts folder — by default `%LOCALAPPDATA%\Microsoft\PowerToys\AdvancedPaste\Scripts`.
   You can change this in **Settings → Advanced Paste → Python scripts → Scripts folder**.
2. Drop a `.py` file into the folder.
3. Add the required header comments at the top (see [Header format](#header-format)).
4. Open the Advanced Paste UI (`Win+Shift+V`) — your script will appear in the action list.

## Header format

Every script must start with one or more **header comment lines**. Each line follows the pattern:

```
# @advancedpaste:<tag>   <value>
```

The parser reads the first 50 lines of each file; only lines beginning with `#` are inspected.

### Supported tags

| Tag | Required | Description |
|-----|----------|-------------|
| `name` | **Yes** | Display name shown in the Advanced Paste UI. |
| `desc` | No | Short description / tooltip. |
| `formats` | No | Comma-separated list of supported clipboard formats. Defaults to **all** formats when omitted. |
| `platform` | No | `windows` (default) or `linux`. Determines the execution mode (see below). |
| `version` | No | Free-form version string (reserved for future use). |
| `requires` | No | Space-separated Python package requirements. See [Declaring dependencies](#declaring-dependencies). |

### Formats

| Value | Clipboard content |
|-------|--------------------|
| `text` | Plain or Unicode text (`CF_UNICODETEXT`) |
| `html` | HTML fragment (`CF_HTML`) |
| `image` | Bitmap / PNG image |
| `audio` | Audio file(s) |
| `video` | Video file(s) |
| `files` or `file` | File paths (`CF_HDROP` / `StorageItems`) |
| `any` | All of the above |

Multiple values can be combined with commas:

```python
# @advancedpaste:formats text,html
```

## Execution modes

### Windows mode (`platform windows`)

The script runs directly on Windows via the configured Python interpreter.
It **owns the clipboard** — use a library like `pywin32` (`win32clipboard`) to read
and write clipboard data.

**Invocation:**

```
python.exe "<script.py>" --format <detected_format> --work-dir "<temp_dir>"
```

**Minimal example — reverse text:**

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

After the script exits with code 0, Advanced Paste re-reads the clipboard and pastes
the result. A non-zero exit code signals failure; stderr is shown in the error UI.

### WSL / Linux mode (`platform linux`)

The script runs inside WSL via `wsl.exe bash -l -c "python3 -X utf8 <script>"`.
Instead of direct clipboard access, data is exchanged via **JSON**:

| Direction | Channel | Schema |
|-----------|---------|--------|
| **Input** (C# → Python) | `stdin` (JSON) | See [Input payload](#input-payload) |
| **Output** (Python → C#) | `stdout` (JSON) | See [Output payload](#output-payload) |

#### Input payload

```jsonc
{
  "version": 2,
  "format": ["text"],           // array of detected clipboard format names
  "work_dir": "/mnt/c/...",     // writable temp directory (WSL path)
  "text": "Hello, world!",      // present when clipboard has text
  "html": "<b>Hello</b>",       // present when clipboard has HTML
  "image_path": "/mnt/c/.../input.png",  // present when clipboard has an image
  "file_paths": ["/mnt/c/.../file.txt"]  // present when clipboard has files
}
```

#### Output payload

```jsonc
{
  "result_type": "text",        // "text" | "html" | "image" | "file" | "files"
  "text": "HELLO, WORLD!",     // for result_type "text"
  "html": "<b>HELLO</b>",      // for result_type "html"
  "image_path": "/mnt/c/.../output.png",  // for result_type "image"
  "file_paths": ["/mnt/c/.../out.txt"]    // for result_type "file"/"files"
}
```

> **Note:** File paths in the output must use `/mnt/<drive>/...` format so that
> Advanced Paste can map them back to Windows paths.

**Minimal example — uppercase text (WSL):**

```python
# @advancedpaste:name   WSL Upper Case
# @advancedpaste:formats text
# @advancedpaste:platform linux
import sys, json

data = json.load(sys.stdin)
text = data.get("text", "")
json.dump({"result_type": "text", "text": text.upper()}, sys.stdout)
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

Multiple tokens on one line are space-separated. You can also use multiple `requires` lines.

### Automatic import detection

Advanced Paste also scans the script body for `import` and `from ... import` statements
and cross-references them against the Python standard library. Any non-stdlib import
that is not already installed triggers a prompt to install it automatically.

A built-in mapping table handles common mismatches (e.g. `win32clipboard` → `pywin32`,
`cv2` → `opencv-python`, `PIL` → `Pillow`). For uncommon packages where the import name
differs from the pip name, add an explicit `requires` entry.

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
- For complex WSL scripts that need packages not available via `apt`, consider using
  a virtual environment. The script can re-exec itself with the venv interpreter:
  ```python
  import os, sys
  venv = os.path.expanduser("~/my_env/bin/python3")
  if os.path.exists(venv) and sys.executable != venv:
      os.execv(venv, [venv] + sys.argv)
  ```
- The `--work-dir` argument (Windows mode) and `work_dir` JSON field (WSL mode) point to
  a temporary directory that is cleaned up after execution. Use it for intermediate files.
