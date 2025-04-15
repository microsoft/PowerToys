// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;

using Microsoft.PowerToys.Run.Plugin.TimeDate.Properties;
using Wox.Plugin;
using Wox.Plugin.Logger;

[assembly: InternalsVisibleTo("Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests")]

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.Components
{
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
            if (!isSystemTimeDate)
            {
                return Resources.ResourceManager.GetString(stringId, CultureInfo.CurrentUICulture) ?? string.Empty;
            }
            else if (!string.IsNullOrEmpty(stringIdNow))
            {
                return Resources.ResourceManager.GetString(stringIdNow, CultureInfo.CurrentUICulture) ?? string.Empty;
            }
            else
            {
                return Resources.ResourceManager.GetString(stringId + "Now", CultureInfo.CurrentUICulture) ?? string.Empty;
            }
        }

        /// <summary>
        /// Copy the given text to the clipboard
        /// </summary>
        /// <param name="text">The text to copy to the clipboard</param>
        /// <returns><see langword="true"/>The text successful copy to the clipboard, otherwise <see langword="false"/></returns>
        /// <remarks>Code copied from TimeZone plugin</remarks>
        internal static bool CopyToClipBoard(in string text)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (Exception exception)
            {
                Log.Exception("Can't copy to clipboard", exception, typeof(ResultHelper));
                MessageBox.Show(exception.Message, Resources.Microsoft_plugin_timedate_copy_failed);
                return false;
            }
        }

        /// <summary>
        /// Create a tool tip for the alternative search tags
        /// </summary>
        /// <param name="result">The <see cref="AvailableResult"/>.</param>
        /// <returns>New <see cref="ToolTipData"/> object or null if <see cref="AvailableResult.AlternativeSearchTag"/> is empty.</returns>
        internal static ToolTipData GetSearchTagToolTip(AvailableResult result, out Visibility visibility)
        {
            switch (string.IsNullOrEmpty(result.AlternativeSearchTag))
            {
                case true:
                    visibility = Visibility.Hidden;
                    return null;
                default:
                    visibility = Visibility.Visible;
                    return new ToolTipData(Resources.Microsoft_plugin_timedate_ToolTipAlternativeSearchTag, result.AlternativeSearchTag);
            }
        }

        /// <summary>
        /// Gets a result with an error message that only numbers can't be parsed
        /// </summary>
        /// <returns>Element of type <see cref="Result"/>.</returns>
        internal static Result CreateNumberErrorResult(string theme, string title, string subtitle) => new Result()
        {
            Title = title,
            SubTitle = subtitle,
            ToolTipData = new ToolTipData(Resources.Microsoft_plugin_timedate_ErrorResultTitle, Resources.Microsoft_plugin_timedate_ErrorResultSubTitle),
            IcoPath = $"Images\\Warning.{theme}.png",
        };
    }
}
