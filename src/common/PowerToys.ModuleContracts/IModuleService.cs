// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Common.UI;

namespace PowerToys.ModuleContracts;

/// <summary>
/// Base contract for PowerToys modules exposed to the Command Palette.
/// </summary>
public interface IModuleService
{
    /// <summary>
    /// Gets module identifier (e.g., Workspaces, Awake).
    /// </summary>
    string Key { get; }

    Task<OperationResult> LaunchAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> OpenSettingsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Helper base to reduce duplication for simple modules.
/// </summary>
public abstract class ModuleServiceBase : IModuleService
{
    public abstract string Key { get; }

    protected abstract SettingsDeepLink.SettingsWindow SettingsWindow { get; }

    public abstract Task<OperationResult> LaunchAsync(CancellationToken cancellationToken = default);

    public virtual Task<OperationResult> OpenSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            SettingsDeepLink.OpenSettings(SettingsWindow);
            return Task.FromResult(OperationResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Fail($"Failed to open settings for {Key}: {ex.Message}"));
        }
    }
}
