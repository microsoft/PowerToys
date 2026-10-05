// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

// UI tests share global desktop state — the same Settings window, the same clipboard, the same
// foreground focus. Parallel execution against shared state is a recipe for non-determinism.
// MSTest defaults to parallel-by-method inside an assembly; pin to sequential here.
[assembly: DoNotParallelize]
