# Copyright (c) Microsoft Corporation. Licensed under the MIT license.
from pathlib import Path
import platform

print("Running on", platform.system())
Path("python-result.txt").write_text("Created by Python inside MXC WSLC\n")
