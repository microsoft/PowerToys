# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# Metadata lives in the sibling py_uppercase_files.py.tool.json descriptor (an MCP Tool). This script
# keeps the Python function convention (files -> files), so it runs through the Python runtime with
# full WSL / path-translation support.

import os


def powerscript_from_files_to_files(file_paths):
    """Write an uppercased copy of each selected file next to the original.

    The function name follows the PowerScripts convention
    ``powerscript_from_<input>_to_<output>``. Because it consumes files and
    produces files, the Explorer right-click menu passes the selected paths in
    and PowerScripts reports the produced paths back.
    """
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
