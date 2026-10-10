// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.MouseWithoutBorders.UITests;

internal enum WinAppSandboxPrerequisite
{
    UserPackageRegistration,
    UserExecutionAlias,
    PackageExecutable,
    ProviderVersion,
}

internal sealed class WinAppSandboxException : InvalidOperationException
{
    public WinAppSandboxException(string code, int? exitCode = null)
        : base($"Modern Sandbox operation failed ({code}{(exitCode is null ? string.Empty : $"; exit {exitCode}")}).")
    {
        Code = code;
        ExitCode = exitCode;
    }

    public WinAppSandboxException(WinAppSandboxPrerequisite prerequisite)
        : base($"BLOCKED_INFRASTRUCTURE: Modern Sandbox provider unavailable for the current interactive test user (trusted_provider_unavailable; {prerequisite}). " +
            PrerequisiteDescription(prerequisite) +
            " An Enabled Windows Sandbox feature or registration for another account is not sufficient. No fallback was attempted.")
    {
        Code = "trusted_provider_unavailable";
    }

    public string Code { get; }

    public int? ExitCode { get; }

    private static string PrerequisiteDescription(WinAppSandboxPrerequisite prerequisite) => prerequisite switch
    {
        WinAppSandboxPrerequisite.UserPackageRegistration =>
            "Exactly one MicrosoftWindows.WindowsSandbox_cw5n1h2txyewy package must be registered for this user.",
        WinAppSandboxPrerequisite.UserExecutionAlias =>
            "This user's WindowsApps wsb.exe app execution alias is missing or is not a registered reparse point.",
        WinAppSandboxPrerequisite.PackageExecutable =>
            "The registered modern Sandbox package does not contain its required wsb.exe executable.",
        WinAppSandboxPrerequisite.ProviderVersion =>
            "The bounded wsb.exe --version probe did not return a successful nonempty version for this user.",
        _ => throw new ArgumentOutOfRangeException(nameof(prerequisite)),
    };
}
