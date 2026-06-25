// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Diagnostics;

namespace PowerDisplay.Contracts;

/// <summary>Single source of truth for the CLI&lt;-&gt;app named-pipe name.
/// Session-scoped so concurrent user sessions never collide; the app is single-instance
/// per session (AppInstance), so the session id alone uniquely identifies the server.</summary>
public static class PipeNames
{
    public static string CliServer()
        => $"PowerDisplay_Cli_Session_{Process.GetCurrentProcess().SessionId}";
}
