#!/usr/bin/env node
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Thin, checked-in launcher for the Command Palette bootstrap. It carries the
// Node shebang so `npm` publishes an executable bin that runs correctly on
// POSIX shells, and delegates to the compiled bootstrap, which claims stdout
// for the protocol before dynamically importing the extension entry. Keeping
// the shebang in this hand-written wrapper avoids depending on the TypeScript
// emitter to preserve one on the compiled output.
import { runBootstrapCli } from '../dist/runtime/bootstrap.js';

await runBootstrapCli();
