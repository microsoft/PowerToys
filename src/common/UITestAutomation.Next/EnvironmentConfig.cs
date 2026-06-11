// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Centralized access to the environment variables that influence UI-test execution. Mirrors the
/// legacy harness's <c>EnvironmentConfig</c> so module tests can branch on pipeline-vs-local and
/// installed-build-vs-dev-build the same way.
/// </summary>
public static class EnvironmentConfig
{
    private static readonly Lazy<bool> InPipeline = new(() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("platform")));

    private static readonly Lazy<bool> UseInstaller = new(() =>
    {
        var raw = Environment.GetEnvironmentVariable("useInstallerForTest")
                  ?? Environment.GetEnvironmentVariable("USEINSTALLERFORTEST");
        return !string.IsNullOrEmpty(raw) && bool.TryParse(raw, out var b) && b;
    });

    private static readonly Lazy<string?> PlatformValue = new(() =>
        Environment.GetEnvironmentVariable("platform"));

    /// <summary>True when running in CI/CD (the <c>platform</c> env var is set).</summary>
    public static bool IsInPipeline => InPipeline.Value;

    /// <summary>True when tests should target the installed PowerToys build (<c>useInstallerForTest</c>).</summary>
    public static bool UseInstallerForTest => UseInstaller.Value;

    /// <summary>Build platform from the <c>platform</c> env var (e.g. <c>x64</c>, <c>arm64</c>), or null locally.</summary>
    public static string? Platform => PlatformValue.Value;
}
