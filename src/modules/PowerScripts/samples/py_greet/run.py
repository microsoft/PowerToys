# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# Greet (Python) — demonstrates prompted PowerScript parameters.
# PowerScripts passes each chosen value as a keyword argument. Values arrive as strings, so the
# boolean parameter is compared against the literal "true".


def powerscript_from_none_to_text(greeting="Hello", name="World", shout="false"):
    message = f"{greeting}, {name}!"
    if str(shout).lower() == "true":
        message = message.upper()

    try:
        import ctypes

        ctypes.windll.user32.MessageBoxW(0, message, "PowerScripts \u2014 py_greet", 0)
    except Exception:
        pass

    return message
