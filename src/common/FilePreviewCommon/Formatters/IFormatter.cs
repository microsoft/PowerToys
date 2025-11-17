// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.FilePreviewCommon.Monaco.Formatters
{
    public interface IFormatter
    {
        /// <summary>
        /// Gets the language to which the formatter is applied
        /// </summary>
        string LangSet { get; }

        /// <summary>
        /// Format the value
        /// </summary>
        /// <param name="value">The value to format</param>
        /// <returns>The value formatted</returns>
        string Format(string value);
    }
}
