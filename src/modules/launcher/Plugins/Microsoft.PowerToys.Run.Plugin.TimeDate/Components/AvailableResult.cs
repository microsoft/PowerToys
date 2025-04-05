// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests")]

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.Components
{
    internal sealed class AvailableResult
    {
        /// <summary>
        /// Gets or sets the time/date value
        /// </summary>
        internal string Value { get; set; }

        /// <summary>
        /// Gets or sets the text used for the subtitle and as search term
        /// </summary>
        internal string Label { get; set; }

        /// <summary>
        /// Gets or sets an alternative search tag that will be evaluated if label doesn't match. For example we like to show the era on searches for 'year' too.
        /// </summary>
        internal string AlternativeSearchTag { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the type of result
        /// </summary>
        internal ResultIconType IconType { get; set; }

        /// <summary>
        /// Returns the path to the icon
        /// </summary>
        /// <param name="theme">Theme</param>
        /// <returns>Path</returns>
        internal string GetIconPath(string theme)
        {
            return IconType switch
            {
                ResultIconType.Time => $"Images\\time.{theme}.png",
                ResultIconType.Date => $"Images\\calendar.{theme}.png",
                ResultIconType.DateTime => $"Images\\timeDate.{theme}.png",
                ResultIconType.Error => $"Images\\Warning.{theme}.png",
                _ => string.Empty,
            };
        }
    }

    internal enum ResultIconType
    {
        Time,
        Date,
        DateTime,
        Error,
    }
}
