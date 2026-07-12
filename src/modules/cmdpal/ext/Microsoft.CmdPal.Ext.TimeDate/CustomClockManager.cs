// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate;

internal sealed class CustomClockManager
{
    private readonly string _filePath;
    private readonly Lock _lock = new();
    private List<CustomClock> _clocks = [];

    internal event EventHandler? ClocksChanged;

    internal CustomClockManager(string? filePath = null)
    {
        _filePath = filePath ?? GetStatePath();
        Load();
    }

    internal IReadOnlyList<CustomClock> Clocks
    {
        get
        {
            lock (_lock)
            {
                return _clocks.ToArray();
            }
        }
    }

    internal void Save(CustomClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Validate(clock);

        lock (_lock)
        {
            var index = _clocks.FindIndex(existing => existing.Id == clock.Id);
            if (index >= 0)
            {
                _clocks[index] = clock;
            }
            else
            {
                _clocks.Add(clock);
            }

            Persist();
        }

        ClocksChanged?.Invoke(this, EventArgs.Empty);
    }

    internal bool Remove(Guid id)
    {
        var removed = false;
        lock (_lock)
        {
            removed = _clocks.RemoveAll(clock => clock.Id == id) > 0;
            if (removed)
            {
                Persist();
            }
        }

        if (removed)
        {
            ClocksChanged?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }

    private static string GetStatePath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "timeDate.customClocks.json");
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            var clocks = JsonSerializer.Deserialize(File.ReadAllText(_filePath), CustomClockJsonContext.Default.ListCustomClock);
            if (clocks is not null)
            {
                _clocks = clocks.Where(IsValid).ToList();
            }
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage($"Failed to load custom clocks: {ex.Message}");
        }
    }

    private void Persist()
    {
        try
        {
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_clocks, CustomClockJsonContext.Default.ListCustomClock));
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage($"Failed to save custom clocks: {ex.Message}");
        }
    }

    private static bool IsValid(CustomClock clock)
    {
        try
        {
            Validate(clock);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void Validate(CustomClock clock)
    {
        if (clock.Id == Guid.Empty)
        {
            throw new ArgumentException("A persisted custom clock needs an ID.");
        }

        if (clock.TimeZoneId != CustomClock.CurrentTimeZoneId)
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(clock.TimeZoneId);
        }

        ValidateFormat(clock.TitleFormat);
        ValidateFormat(clock.SubtitleFormat);
    }

    private static void ValidateFormat(string format)
    {
        if (!string.IsNullOrEmpty(format) &&
            !format.StartsWith("UTC:", StringComparison.Ordinal) &&
            !TimeAndDateHelper.StringContainsCustomFormatSyntax(format))
        {
            _ = DateTime.UtcNow.ToString(CustomClockDisplay.NormalizeRelativeDayToken(format).Replace("REL", "'Today'", StringComparison.Ordinal), CultureInfo.CurrentCulture);
        }
    }
}
