// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace PowerScripts.Core.Security;

/// <summary>The exit code, standard output and standard error captured from a launched process.</summary>
public readonly record struct ProcessRunResult(int ExitCode, string StdOut, string StdErr);
