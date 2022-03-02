// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.Components
{
    public class AvailableResult
    {
        /// <summary>
        /// Gets or sets the time/date value
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Gets or sets the text used for the subtitle and as search term
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the type of result
        /// </summary>
        public ResultIconType IconType { get; set; }

        /// <summary>
        /// Returns the path to the icon
        /// </summary>
        /// <param name="theme">Theme</param>
        /// <returns>Path</returns>
        public string GetIconPath(string theme)
        {
            switch (IconType)
            {
                case ResultIconType.Time:
                    return $"Images\\time.{theme}.png";
                case ResultIconType.Date:
                    return $"Images\\calendar.{theme}.png";
                case ResultIconType.DateTime:
                    return $"Images\\timeDate.{theme}.png";
                default:
                    return string.Empty;
            }
        }
    }

    public enum ResultIconType
    {
        Time,
        Date,
        DateTime,
    }
}
