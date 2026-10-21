# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# Entry function and I/O are declared in py_uppercase_files.py.tool.json.

import os


def uppercase_files(file_paths):
    """Write an uppercased copy of each selected file next to the original."""
    outputs = []
    for path in file_paths:
        root, _ext = os.path.splitext(path)
        out_path = root + ".UPPER.txt"
        with open(path, "r", encoding="utf-8") as source:
            content = source.read()
        with open(out_path, "w", encoding="utf-8") as target:
            target.write(content.upper())
        outputs.append(out_path)
    return outputs
