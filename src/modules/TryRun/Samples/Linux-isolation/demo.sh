#!/bin/sh
# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.
set -eu
printf 'Linux isolation demo: reading the copied input.\n'
cat notes.txt
printf 'Changed inside the Linux run copy.' > notes.txt
printf 'Created inside the Linux container.' > linux-result.txt
host_result='not tested'
if [ -n "${TRYRUN_HOST_PROBE:-}" ]; then
    if cat "$TRYRUN_HOST_PROBE" >/dev/null 2>&1; then
        host_result='unexpectedly readable'
    else
        host_result='blocked or unavailable'
    fi
fi
network_result='external interface observed'
if [ "$(ls /sys/class/net)" = 'lo' ]; then
    network_result='only loopback observed'
fi
cat > tryrun-observations.json <<EOF
{"version":1,"observations":[
{"resource":"notes.txt and linux-result.txt","access":"write","outcome":"succeeded","detail":"The script modified a copied input and created a result beside it."},
{"resource":"Harmless host-only fixture","access":"read","outcome":"$host_result","detail":"The demo button supplies a host-verified fixture outside the mounted folders. This row is the script's observation, not a native MXC denial event."},
{"resource":"/sys/class/net","access":"inspect","outcome":"$network_result","detail":"Observed interface names. This is not a complete network-isolation test."}
]}
EOF
printf 'Host fixture: %s\nInterfaces: %s\n' "$host_result" "$network_result"
printf 'Done. Review changes and the isolation report before exporting.\n'
