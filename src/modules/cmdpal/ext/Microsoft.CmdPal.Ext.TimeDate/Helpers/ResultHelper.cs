// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate.Helpers;

internal static class ResultHelper
{
    /// <summary>
    /// Get the string based on the requested type
    /// </summary>
    /// <param name="isSystemTimeDate">Does the user search for system date/time?</param>
    /// <param name="stringId">Id of the string. (Example: `MyString` for `MyString` and `MyStringNow`)</param>
    /// <param name="stringIdNow">Optional string id for now case</param>
    /// <returns>The string from the resource file, or <see cref="string.Empty"/> otherwise.</returns>
    internal static string SelectStringFromResources(bool isSystemTimeDate, string stringId, string stringIdNow = default)
    {
        return !isSystemTimeDate
            ? Resources.ResourceManager.GetString(stringId, CultureInfo.CurrentUICulture) ?? string.Empty
            : !string.IsNullOrEmpty(stringIdNow)
                ? Resources.ResourceManager.GetString(stringIdNow, CultureInfo.CurrentUICulture) ?? string.Empty
                : Resources.ResourceManager.GetString(stringId + "Now", CultureInfo.CurrentUICulture) ?? string.Empty;
    }

    public static IconInfo TimeIcon { get; } = new IconInfo("\uE823");

    public static IconInfo CalendarIcon { get; } = new IconInfo("\uE787");

    public static IconInfo TimeDateIcon { get; } = new IconInfo("\uEC92");

    /// <summary>
    /// Gets a result with an error message that only numbers can't be parsed
    /// </summary>
    /// <returns>Element of type <see cref="Result"/>.</returns>
    internal static ListItem CreateNumberErrorResult() => new ListItem(new NoOpCommand())
    {
        Title = Resources.Microsoft_plugin_timedate_ErrorResultTitle,
        Subtitle = Resources.Microsoft_plugin_timedate_ErrorResultSubTitle,
        Icon = IconHelpers.FromRelativePaths("Microsoft.CmdPal.Ext.TimeDate\\Assets\\Warning.light.png", "Microsoft.CmdPal.Ext.TimeDate\\Assets\\Warning.dark.png"),
    };

    internal static ListItem CreateInvalidInputErrorResult() => new ListItem(new NoOpCommand())
    {
        Title = Resources.Microsoft_plugin_timedate_InvalidInput_ErrorMessageTitle,
        Subtitle = Resources.Microsoft_plugin_timedate_InvalidInput_ErrorMessageSubTitle,
        Icon = IconHelpers.FromRelativePaths("Microsoft.CmdPal.Ext.TimeDate\\Assets\\Warning.light.png", "Microsoft.CmdPal.Ext.TimeDate\\Assets\\Warning.dark.png"),
    };
}
