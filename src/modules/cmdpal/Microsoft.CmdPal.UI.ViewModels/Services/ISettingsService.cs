// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Manages the lifecycle of <see cref="SettingsModel"/>: load, save, migration, and change notification.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current settings instance.
    /// </summary>
    SettingsModel Settings { get; }

    /// <summary>
    /// Persists the current settings to disk.
    /// </summary>
    /// <param name="hotReload">When <see langword="true"/>, raises <see cref="SettingsChanged"/> after saving.</param>
    void Save(bool hotReload = true);

    /// <summary>
    /// Reloads settings from disk, replacing the current instance.
    /// </summary>
    void Reload();

    /// <summary>
    /// Raised after settings are saved with <paramref name="hotReload"/> enabled, or after <see cref="Reload"/>.
    /// </summary>
    event TypedEventHandler<ISettingsService, SettingsModel> SettingsChanged;
}
