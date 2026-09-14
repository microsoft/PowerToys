#!/bin/sh
# Copyright (c) Microsoft Corporation. Licensed under the MIT license.
set -eu
uname -s
printf 'Created inside the Linux container\n' > linux-result.txt
if [ -f notes.txt ]; then
    printf 'Changed only in the copied workspace\n' > notes.txt
fi
ls -la
