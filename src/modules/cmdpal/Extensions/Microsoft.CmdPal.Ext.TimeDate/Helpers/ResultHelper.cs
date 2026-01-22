// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.CommandPalette.Extensions;
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

    /// <summary>
    /// Gets a result with an error message that input can't be parsed
    /// </summary>
    /// <returns>Element of type <see cref="Result"/>.</returns>
#pragma warning disable CA1863 // Use 'CompositeFormat'
    internal static ListItem CreateInvalidInputErrorResult() => new ListItem(new NoOpCommand())
    {
        Title = Resources.Microsoft_plugin_timedate_InvalidInput_ErrorMessageTitle,
        Icon = Icons.ErrorIcon,
        Details = new Details()
        {
            Title = Resources.Microsoft_plugin_timedate_InvalidInput_DetailsHeader,

            // Because of translation we can't use 'CompositeFormat'.
            Body = string.Format(CultureInfo.CurrentCulture, Resources.Microsoft_plugin_timedate_InvalidInput_SupportedInput, "**", "\n\n", "\n\n* "),
        },
    };
#pragma warning restore CA1863 // Use 'CompositeFormat'
}
