# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

import json
import sys

request = json.load(sys.stdin)
print(request["text"].upper())
