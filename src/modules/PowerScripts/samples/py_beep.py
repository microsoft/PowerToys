# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# Entry function and I/O are declared in py_beep.py.tool.json.

import sys


def beep():
    """Play a short beep.

    This is a Python *system* PowerScript: it takes no clipboard/file input and
    produces no output (``none`` -> ``none``), so it is a good fit for a Keyboard
    Manager hotkey. winsound is Windows-only stdlib; fall back to the terminal
    bell elsewhere so the script still runs.
    """
    try:
        import winsound

        winsound.Beep(880, 200)
    except Exception:
        sys.stdout.write("\a")
