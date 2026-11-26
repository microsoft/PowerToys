// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading;

using ManagedCommon;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.UI.Dispatching;

using MouseJump.Common.Helpers;
using MouseJump.Models.Drawing;
using MouseJump.Models.Settings;
using MouseJump.Models.Styles;

namespace MouseJump.WinUI3.Helpers;

internal sealed class SettingsHelper : IDisposable
{
    private readonly IFileSystemWatcher _fileSystemWatcher;
    private readonly DispatcherQueueTimer _reloadTimer;
    private volatile MouseJumpSettings _currentSettings;
    private bool _disposed;

    public SettingsHelper(DispatcherQueue dispatcherQueue)
    {
        _currentSettings = LoadSettings();

        // Give the writer time to release the settings file before reloading it.
        _reloadTimer = dispatcherQueue.CreateTimer();
        _reloadTimer.Interval = TimeSpan.FromMilliseconds(250);
        _reloadTimer.IsRepeating = false;
        _reloadTimer.Tick += this.ReloadTimer_Tick;

        _fileSystemWatcher = Helper.GetFileWatcher(
            moduleName: MouseJumpSettings.ModuleName,
            fileName: "settings.json",
            onChangedCallback: () => dispatcherQueue.TryEnqueue(this.RestartReloadTimer));
    }

    public MouseJumpSettings CurrentSettings => _currentSettings;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _fileSystemWatcher.Dispose();
        _reloadTimer.Stop();
        _reloadTimer.Tick -= this.ReloadTimer_Tick;
    }

    private void RestartReloadTimer()
    {
        if (_disposed)
        {
            return;
        }

        _reloadTimer.Stop();
        _reloadTimer.Start();
    }

    private void ReloadTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        _currentSettings = LoadSettings();
    }

    private static MouseJumpSettings LoadSettings()
    {
        var settingsUtils = SettingsUtils.Default;

        // set this to 1 to disable retries
        var remainingRetries = 5;

        while (remainingRetries > 0)
        {
            try
            {
                if (!settingsUtils.SettingsExists(MouseJumpSettings.ModuleName))
                {
                    Logger.LogInfo("MouseJump settings.json was missing, creating a new one");
                    var defaultSettings = new MouseJumpSettings();
                    defaultSettings.Save(settingsUtils);
                }

                var settings = settingsUtils.GetSettingsOrDefault<MouseJumpSettings>(MouseJumpSettings.ModuleName);
                return settings;
            }
            catch (IOException ex)
            {
                Logger.LogError("Failed to read changed settings", ex);
                Thread.Sleep(250);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to read changed settings", ex);
                Thread.Sleep(250);
            }

            remainingRetries--;
        }

        const string message = "Failed to read changed settings - ran out of retries";
        Logger.LogError(message);
        throw new InvalidOperationException(message);
    }

    public static PreviewStyle GetActivePreviewStyle(MouseJumpSettings settings)
    {
        var previewType = Enum.TryParse<PreviewType>(settings.Properties.PreviewType, true, out var previewTypeResult)
            ? previewTypeResult
            : PreviewType.Bezelled;

        var canvasSize = new SizeInfo(
            settings.Properties.ThumbnailSize.Width,
            settings.Properties.ThumbnailSize.Height);

        var properties = settings.Properties;

        var previewStyle = previewType switch
        {
            PreviewType.Compact => StyleHelper.CompactPreviewStyle.WithCanvasSize(canvasSize),
            PreviewType.Bezelled => StyleHelper.BezelledPreviewStyle.WithCanvasSize(canvasSize),
            PreviewType.Custom => new PreviewStyle(
                canvasSize: canvasSize,
                canvasStyle: new(
                    marginStyle: new(0),
                    borderStyle: new(
                        color: ColorHelper.DeserializeFromConfigColorString(
                            properties.BorderColor),
                        all: properties.BorderThickness,
                        depth: properties.Border3dDepth
                    ),
                    paddingStyle: new(
                        all: properties.BorderPadding
                    ),
                    backgroundStyle: new(
                        color1: ColorHelper.DeserializeFromConfigColorString(
                            properties.BackgroundColor1),
                        color2: ColorHelper.DeserializeFromConfigColorString(
                            properties.BackgroundColor2)
                    )
                ),
                screenStyle: new(
                    marginStyle: new(
                        all: properties.ScreenMargin
                    ),
                    borderStyle: new(
                        color: ColorHelper.DeserializeFromConfigColorString(
                            properties.BezelColor),
                        all: properties.BezelThickness,
                        depth: properties.Bezel3dDepth
                    ),
                    paddingStyle: new(0),
                    backgroundStyle: new(
                        color1: ColorHelper.DeserializeFromConfigColorString(
                            properties.ScreenColor1),
                        color2: ColorHelper.DeserializeFromConfigColorString(
                            properties.ScreenColor2)
                    )
                ),
                extraColors: []),
            _ => throw new InvalidOperationException(
                $"Unhandled {nameof(PreviewType)} '{previewType}'"),
        };

        return previewStyle;
    }
}
