// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// A stand-in for an extension entry that emits raw bytes at module-evaluation
// time. When loaded through the bootstrap loader, these top-level writes must be
// redirected away from the protocol channel and land on stderr instead.
console.log('top-level-console-log');
process.stdout.write('top-level-stdout-write\n');

export const loaded = true;
