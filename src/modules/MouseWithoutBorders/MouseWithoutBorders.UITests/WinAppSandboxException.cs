// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.MouseWithoutBorders.UITests;

internal sealed class WinAppSandboxException(string code, int? exitCode = null)
    : InvalidOperationException($"Modern Sandbox operation failed ({code}{(exitCode is null ? string.Empty : $"; exit {exitCode}")}).")
{
    public string Code { get; } = code;

    public int? ExitCode { get; } = exitCode;
}
