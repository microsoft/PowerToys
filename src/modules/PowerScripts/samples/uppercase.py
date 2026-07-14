# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# @powerscript.id           uppercase
# @powerscript.name         Uppercase Text
# @powerscript.description   Convert clipboard text to UPPERCASE. A Python PowerScript usable from Advanced Paste or a hotkey.
# @powerscript.kind         system
# @powerscript.publisher    PowerToys samples
# @powerscript.version      1.0.0
# @powerscript.surface      advancedPaste keyboardManager


def powerscript_from_text_to_text(text: str) -> str:
    """Return the input text converted to uppercase.

    The function name follows the PowerScripts convention
    ``powerscript_from_<input>_to_<output>``: it consumes clipboard text and
    produces text, so Advanced Paste offers it whenever the clipboard has text.
    """
    return text.upper()
