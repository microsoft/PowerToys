# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# @powerscript.id           py_greet
# @powerscript.name         Greet (Python)
# @powerscript.description   Show a greeting. Demonstrates prompted parameters passed to a Python PowerScript.
# @powerscript.kind         system
# @powerscript.publisher    PowerToys samples
# @powerscript.version      1.0.0
# @powerscript.capability   ui
# @powerscript.prompt       true
# @powerscript.param        name=greeting type=choice label="Greeting" description="Pick how to say hello." options=Hello,Hi,Hey,Howdy default=Hello
# @powerscript.param        name=name type=string label="Name" description="Who to greet." default=World
# @powerscript.param        name=shout type=bool label="Shout (UPPERCASE)" default=false
#
# Greet (Python) — demonstrates prompted PowerScript parameters.
# PowerScripts passes each chosen value as a keyword argument. Values arrive as strings, so the
# boolean parameter is compared against the literal "true".


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
