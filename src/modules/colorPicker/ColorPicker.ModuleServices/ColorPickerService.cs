// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json;
using Common.UI;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.Interop;
using PowerToys.ModuleContracts;

namespace ColorPicker.ModuleServices;

/// <summary>
/// Provides programmatic control for Color Picker actions.
/// </summary>
public sealed class ColorPickerService : ModuleServiceBase, IColorPickerService
{
    public static ColorPickerService Instance { get; } = new();

    public override string Key => SettingsDeepLink.SettingsWindow.ColorPicker.ToString();

    protected override SettingsDeepLink.SettingsWindow SettingsWindow => SettingsDeepLink.SettingsWindow.ColorPicker;

    public override Task<OperationResult> LaunchAsync(CancellationToken cancellationToken = default)
    {
        // Default launch -> open picker.
        return OpenPickerAsync(cancellationToken);
    }

    public Task<OperationResult> OpenPickerAsync(CancellationToken cancellationToken = default)
    {
        return SignalEventAsync(Constants.ShowColorPickerSharedEvent(), "Color Picker");
    }

    public Task<OperationResult<IReadOnlyList<SavedColor>>> GetSavedColorsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var historyPath = Path.Combine(localAppData, "Microsoft", "PowerToys", "ColorPicker", "colorHistory.json");
            if (!File.Exists(historyPath))
            {
                return Task.FromResult(OperationResults.Ok<IReadOnlyList<SavedColor>>(Array.Empty<SavedColor>()));
            }

            using var stream = File.OpenRead(historyPath);
            var colors = JsonSerializer.Deserialize(stream, ColorPickerServiceJsonContext.Default.ListString) ?? new List<string>();

            var settingsUtils = new SettingsUtils();
            var settings = settingsUtils.GetSettingsOrDefault<ColorPickerSettings>(ColorPickerSettings.ModuleName);

            var results = new List<SavedColor>(colors.Count);
            foreach (var entry in colors)
            {
                if (!TryParseArgb(entry, out var color))
                {
                    continue;
                }

                var formats = BuildFormats(color, settings);
                var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

                results.Add(new SavedColor(
                    hex,
                    color.A,
                    color.R,
                    color.G,
                    color.B,
                    formats));
            }

            return Task.FromResult(OperationResults.Ok<IReadOnlyList<SavedColor>>(results));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(OperationResults.Fail<IReadOnlyList<SavedColor>>("Reading saved colors was cancelled."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResults.Fail<IReadOnlyList<SavedColor>>($"Failed to read saved colors: {ex.Message}"));
        }
    }

    private static Task<OperationResult> SignalEventAsync(string eventName, string actionDescription)
    {
        try
        {
            using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            if (!eventHandle.Set())
            {
                return Task.FromResult(OperationResult.Fail($"Failed to signal {actionDescription}."));
            }

            return Task.FromResult(OperationResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Fail($"Failed to signal {actionDescription}: {ex.Message}"));
        }
    }

    private static bool TryParseArgb(string value, out Color color)
    {
        color = Color.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('|');
        if (parts.Length != 4)
        {
            return false;
        }

        if (byte.TryParse(parts[0], out var a) &&
            byte.TryParse(parts[1], out var r) &&
            byte.TryParse(parts[2], out var g) &&
            byte.TryParse(parts[3], out var b))
        {
            color = Color.FromArgb(a, r, g, b);
            return true;
        }

        return false;
    }

    private static IReadOnlyList<ColorFormatValue> BuildFormats(Color color, ColorPickerSettings settings)
    {
        var formats = new List<ColorFormatValue>();
        foreach (var kvp in settings.Properties.VisibleColorFormats)
        {
            var formatName = kvp.Key;
            var (isVisible, formatString) = kvp.Value;
            if (!isVisible)
            {
                continue;
            }

            var formatted = ColorFormatHelper.GetStringRepresentation(color, formatString);
            if (formatName.Equals("HEX", StringComparison.OrdinalIgnoreCase) && !formatted.StartsWith('#'))
            {
                formatted = "#" + formatted;
            }

            formats.Add(new ColorFormatValue(formatName, formatted));
        }

        return formats;
    }
}
