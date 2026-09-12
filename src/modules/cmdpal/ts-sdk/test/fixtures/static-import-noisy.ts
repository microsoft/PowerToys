// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Emits raw bytes at module-evaluation time. A user entry that statically
// imports this module triggers these writes during import hoisting, before the
// entry's own body runs. Loaded through the bootstrap loader, they must be
// redirected off the protocol channel and land on stderr instead.
console.log('static-import-console-log');
process.stdout.write('static-import-stdout-write\n');

export const marker = 'static-import-noisy';
