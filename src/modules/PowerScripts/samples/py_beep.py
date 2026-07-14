# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# @powerscript.id           py_beep
# @powerscript.name         Beep (Python)
# @powerscript.description   Play a short beep. A Python system PowerScript you can bind to a hotkey from Keyboard Manager.
# @powerscript.kind         system
# @powerscript.publisher    PowerToys samples
# @powerscript.version      1.0.0
# @powerscript.capability   systemControl

import sys


def powerscript_from_none_to_none():
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
