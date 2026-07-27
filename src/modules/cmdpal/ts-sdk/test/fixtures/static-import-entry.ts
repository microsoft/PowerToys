// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// A stand-in for an extension entry whose only top-level output happens through
// a statically imported module. ES module static imports are hoisted and
// evaluated before this file's body, so the noisy module writes before any code
// here runs. When launched through the bootstrap loader, the whole graph is
// imported only after stdout is claimed, so even these hoisted writes must not
// reach the protocol channel.
import './static-import-noisy.js';

export const loaded = true;
