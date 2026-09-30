# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# Greet (Python) — demonstrates consumer-rendered PowerScript parameters. Metadata (including the parameters)
# lives in the sibling py_greet.py.tool.json descriptor (an MCP Tool). This script keeps the Python
# function convention, so it runs through the Python runtime (WSL-capable). PowerScripts passes each
# chosen value as a keyword argument. Values arrive as strings, so the boolean parameter is compared
# against the literal "true".


def powerscript_from_none_to_text(greeting="Hello", name="World", shout="false"):
    message = f"{greeting}, {name}!"
    if str(shout).lower() == "true":
        message = message.upper()

    try:
        import ctypes

        # Force the result box above the foreground window. MB_TOPMOST alone is unreliable, so combine
        # MB_SYSTEMMODAL (0x1000) | MB_SETFOREGROUND (0x10000) | MB_TOPMOST (0x40000).
        MB_TOPMOST_FLAGS = 0x00051000
        ctypes.windll.user32.MessageBoxW(0, message, "PowerScripts \u2014 py_greet", MB_TOPMOST_FLAGS)
    except Exception:
        pass

    return message
