# Optional transport examples

Scaffold the stated I/O with the Host; retain its descriptor and implement
the actual task/options. These examples do not justify changing Python mode.
Choose one implementation with the matching sibling descriptor filename.

## Text to text

```powershell
param([string]$Text)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::Out.Write($Text.ToUpperInvariant())
```

```python
import json
import sys

request = json.load(sys.stdin)
sys.stdout.write(request["text"].upper())
```

Stdout is literal text; the Host wraps it for `transform`.
Empty output is rejected by Advanced Paste; define task-specific empty behavior.

## Files to a separate report file (Python)

Scaffold `input: files`, `output: files` and appropriate count/extension limits.

```python
import json
import sys
import tempfile
from pathlib import Path

request = json.load(sys.stdin)
with tempfile.NamedTemporaryFile(
    mode="w", encoding="utf-8", suffix=".txt", delete=False
) as report:
    for selected in request["files"]:
        path = Path(selected)
        report.write(f"{path.name}\t{path.stat().st_size} bytes\n")
json.dump({"files": [report.name]}, sys.stdout)
```

Close the file before returning it; `tempfile` uses the Host's Linux environment.
No original-file writes or hardcoded WSL paths. Operational failures stay nonzero;
see [errors](error-handling.md) when adding task-specific diagnostics.
