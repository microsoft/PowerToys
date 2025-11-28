// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Common.UI;
using ManagedCommon;
using PowerToys.ModuleContracts;

namespace Awake.ModuleServices;

/// <summary>
/// Provides CLI-based Awake control for reuse across hosts.
/// </summary>
public sealed class AwakeService : ModuleServiceBase, IAwakeService
{
    public static AwakeService Instance { get; } = new();

    public override string Key => SettingsDeepLink.SettingsWindow.Awake.ToString();

    protected override SettingsDeepLink.SettingsWindow SettingsWindow => SettingsDeepLink.SettingsWindow.Awake;

    public override Task<OperationResult> LaunchAsync(CancellationToken cancellationToken = default)
    {
        // Default launch -> indefinite, honoring Awake's own settings for display behavior.
        return SetIndefiniteAsync(cancellationToken);
    }

    public Task<OperationResult> SetIndefiniteAsync(CancellationToken cancellationToken = default)
    {
        return InvokeCliAsync("-m indefinite");
    }

    public Task<OperationResult> SetTimedAsync(int minutes, CancellationToken cancellationToken = default)
    {
        if (minutes <= 0)
        {
            return Task.FromResult(OperationResult.Fail("Minutes must be greater than zero."));
        }

        return InvokeCliAsync($"-m timed -t {minutes}");
    }

    public Task<OperationResult> SetOffAsync(CancellationToken cancellationToken = default)
    {
        return InvokeCliAsync("-m passive");
    }

    private static Task<OperationResult> InvokeCliAsync(string arguments)
    {
        try
        {
            var basePath = PowerToysPathResolver.GetPowerToysInstallPath();
            if (string.IsNullOrWhiteSpace(basePath))
            {
                return Task.FromResult(OperationResult.Fail("PowerToys install path not found."));
            }

            var exePath = Path.Combine(basePath, "PowerToys.Awake.exe");
            if (!File.Exists(exePath))
            {
                return Task.FromResult(OperationResult.Fail("Unable to locate PowerToys.Awake.exe."));
            }

            var startInfo = new ProcessStartInfo(exePath, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            Process.Start(startInfo);
            return Task.FromResult(OperationResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Fail($"Failed to invoke Awake: {ex.Message}"));
        }
    }
}
