// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Microsoft.PowerToys.Run.Plugin.TimeZone.Extensions
{
    /// <summary>
    /// Extensions for <see cref="StringBuilder"/>-Objects
    /// </summary>
    internal static class StringBuilderExtensions
    {
        /// <summary>
        /// Save append the given <see cref="string"/> value with the given maximum length to the <see cref="StringBuilder"/>
        /// </summary>
        /// <param name="stringBuilder">The <see cref="StringBuilder"/> to append the string.</param>
        /// <param name="value">The value that should be append.</param>
        /// <param name="maxLength">The max length of the <see cref="string"/> value that should append.</param>
        internal static void SaveAppend(this StringBuilder stringBuilder, string value, int maxLength)
        {
            if (value.Length > maxLength)
            {
                stringBuilder.Append(value, 0, maxLength);
            }
            else
            {
                stringBuilder.Append(value);
            }
        }

        /// <summary>
        /// Cut too long texts to the given length and add three dots at the end of the text.
        /// </summary>
        /// <param name="stringBuilder">The <see cref="StringBuilder"/> that contain the text.</param>
        /// <param name="maxLength">The maximum length for the text, inclusive the three dots.</param>
        internal static void CutTooLong(this StringBuilder stringBuilder, int maxLength)
        {
            if (stringBuilder.Length <= maxLength)
            {
                return;
            }

            stringBuilder.Length = maxLength - 3;
            stringBuilder.Append('.');
            stringBuilder.Append('.');
            stringBuilder.Append('.');
        }
    }
}
