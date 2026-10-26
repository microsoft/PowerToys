# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

import json
import sys

json.load(sys.stdin)
if sys.platform == "win32":
    import winsound

    winsound.Beep(880, 200)
else:
    print("\a", end="", file=sys.stderr)
