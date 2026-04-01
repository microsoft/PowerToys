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
using MouseJump.Common.Helpers;
using MouseJump.Common.Models.Drawing;
using MouseJump.Common.Models.Settings;
using MouseJump.Common.Models.Styles;

namespace MouseJumpUI.Helpers;

internal sealed class SettingsHelper
{
    public SettingsHelper()
    {
        this.LockObject = new();
        this.CurrentSettings = this.LoadSettings();

        // delay loading settings on change by some time to avoid file in use exception
        var throttledActionInvoker = new ThrottledActionInvoker();
        this.FileSystemWatcher = Helper.GetFileWatcher(
            moduleName: MouseJumpSettings.ModuleName,
            fileName: "settings.json",
            onChangedCallback: () => throttledActionInvoker.ScheduleAction(this.ReloadSettings, 250));
    }

    private IFileSystemWatcher FileSystemWatcher
    {
        get;
    }

    private object LockObject
    {
        get;
    }

    public MouseJumpSettings CurrentSettings
    {
        get;
        private set;
    }

    private MouseJumpSettings LoadSettings()
    {
        lock (this.LockObject)
        {
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
            }
        }

        const string message = "Failed to read changed settings - ran out of retries";
        Logger.LogError(message);
        throw new InvalidOperationException(message);
    }

    public void ReloadSettings()
    {
        this.CurrentSettings = this.LoadSettings();
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
                        color: ConfigHelper.DeserializeFromConfigColorString(
                            properties.BorderColor),
                        all: properties.BorderThickness,
                        depth: properties.Border3dDepth
                    ),
                    paddingStyle: new(
                        all: properties.BorderPadding
                    ),
                    backgroundStyle: new(
                        color1: ConfigHelper.DeserializeFromConfigColorString(
                            properties.BackgroundColor1),
                        color2: ConfigHelper.DeserializeFromConfigColorString(
                            properties.BackgroundColor2)
                    )
                ),
                screenStyle: new(
                    marginStyle: new(
                        all: properties.ScreenMargin
                    ),
                    borderStyle: new(
                        color: ConfigHelper.DeserializeFromConfigColorString(
                            properties.BezelColor),
                        all: properties.BezelThickness,
                        depth: properties.Bezel3dDepth
                    ),
                    paddingStyle: new(0),
                    backgroundStyle: new(
                        color1: ConfigHelper.DeserializeFromConfigColorString(
                            properties.ScreenColor1),
                        color2: ConfigHelper.DeserializeFromConfigColorString(
                            properties.ScreenColor2)
                    )
                )),
            _ => throw new InvalidOperationException(
                $"Unhandled {nameof(PreviewType)} '{previewType}'"),
        };

        return previewStyle;
    }
}
