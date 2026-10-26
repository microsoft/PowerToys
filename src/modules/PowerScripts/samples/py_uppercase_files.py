# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

import json
import sys
import tempfile
from pathlib import Path

request = json.load(sys.stdin)
outputs = []
for path in request["files"]:
    with tempfile.NamedTemporaryFile(mode="w", suffix=".UPPER.txt", encoding="utf-8", delete=False) as output:
        output.write(Path(path).read_text(encoding="utf-8").upper())
        outputs.append(output.name)
print(json.dumps({"files": outputs}))
