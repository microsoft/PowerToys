// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.PowerToys.Run.Plugin.TimeDate.Properties;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.Components
{
    internal static class ResultHelper
    {
        /// <summary>
        /// Returns a list with all available commands
        /// </summary>
        /// <param name="iconTheme">Them for the icon</param>
        /// <returns>List of results</returns>
        internal static List<Result> GetCommandList(string iconTheme)
        {
            List<Result> results = new List<Result>();
            DateTime dateTimeNow = DateTime.Now;
            long unixTimestamp = (long)dateTimeNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

            results.AddRange(new[]
            {
                new Result()
                {
                    Title = dateTimeNow.ToString(GetStringFormat(FormatType.Time)),
                    SubTitle = $"{Resources.Microsoft_plugin_timedate_time} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                    IcoPath = $"Images\\time.{iconTheme}.png",
                    Action = _ => TryToCopyToClipBoard(dateTimeNow.ToString(GetStringFormat(FormatType.Time))),
                },
                new Result()
                {
                    Title = unixTimestamp.ToString(),
                    SubTitle = $"{Resources.Microsoft_plugin_timedate_timeUnix} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                    IcoPath = $"Images\\time.{iconTheme}.png",
                    Action = _ => TryToCopyToClipBoard(unixTimestamp.ToString()),
                },
            });

            return results;
        }

        /// <summary>
        /// Copy the given text to the clipboard
        /// </summary>
        /// <param name="text">The text to copy to the clipboard</param>
        /// <returns><see langword="true"/>The text successful copy to the clipboard, otherwise <see langword="false"/></returns>
        /// <remarks>Code copied from TimeZone plugin</remarks>
        internal static bool TryToCopyToClipBoard(in string text)
        {
            try
            {
                Clipboard.Clear();
                Clipboard.SetText(text);
                return true;
            }
            catch (Exception exception)
            {
                Log.Exception("Can't copy to clipboard", exception, typeof(Main));
                return false;
            }
        }

        /// <summary>
        /// Get the format for the time string
        /// </summary>
        /// <param name="targetFormat">Type of format</param>
        /// <returns>Stirng that identifies the tiem/date format</returns>
        private static string GetStringFormat(FormatType targetFormat)
        {
            switch (targetFormat)
            {
                case FormatType.Time:
                    return TimeDateSettings.Instance.TimeWithSeconds ? "T" : "t";
                case FormatType.Date:
                    return TimeDateSettings.Instance.DateWithWeekday ? "D" : "d";
                case FormatType.DateTime:
                    if (TimeDateSettings.Instance.TimeWithSeconds & TimeDateSettings.Instance.DateWithWeekday)
                    {
                        return "d";
                    }
                    else if (TimeDateSettings.Instance.TimeWithSeconds & !TimeDateSettings.Instance.DateWithWeekday)
                    {
                        return "d";
                    }
                    else if (!TimeDateSettings.Instance.TimeWithSeconds & TimeDateSettings.Instance.DateWithWeekday)
                    {
                        return "d";
                    }
                    else
                    {
                        // (!TimeDateSettings.Instance.TimeWithSeconds & !TimeDateSettings.Instance.DateWithWeekday)
                        return "d";
                    }

                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Type of time format
        /// </summary>
        private enum FormatType
        {
            Time,
            Date,
            DateTime,
        }
    }
}
