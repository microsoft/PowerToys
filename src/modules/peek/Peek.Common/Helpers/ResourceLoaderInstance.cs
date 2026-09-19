// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Peek.Common.Helpers
{
    public static class ResourceLoaderInstance
    {
        public static readonly Lazy<ResourceLoader?> _resourceLoader = new(() =>
        {
            try
            {
                return new ResourceLoader("PowerToys.Peek.UI.pri");
            }
            catch
            {
                // Fallback for unit tests.
                return null;
            }
        });

        public static ResourceLoader? ResourceLoader => _resourceLoader.Value;

        /// <summary>
        /// Safely retrieve a localized resource string, returning a fallback value or
        /// empty string if the resource is not found or an error occurs.
        /// </summary>
        /// <param name="resourceKey">The key of the resource string to retrieve.</param>
        /// <param name="fallback">The fallback value to return if the resource is not
        /// found or an error occurs.</param>
        /// <returns>The localized resource string or the fallback value.</returns>
        public static string GetString(string resourceKey, string fallback = "")
        {
            try
            {
                return ResourceLoader?.GetString(resourceKey) ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>
        /// Formats a localized resource string with the specified arguments, using
        /// CurrentCulture by default.
        /// </summary>
        /// <param name="resourceKey">The key of the resource string to format.</param>
        /// <param name="args">The arguments with which to format the string.</param>
        /// <returns>The formatted localized resource string.</returns>
        public static string FormatString(string resourceKey, params object?[] args) =>
            FormatString(resourceKey, CultureInfo.CurrentCulture, args);

        /// <summary>
        /// Formats a localized resource string with the specified arguments and culture.
        /// </summary>
        /// <param name="resourceKey">The key of the resource string to format.</param>
        /// <param name="culture">The culture to use when formatting the string.</param>
        /// <param name="args">The arguments with which to format the string.</param>
        /// <returns>The formatted localized resource string.</returns>
        public static string FormatString(string resourceKey, CultureInfo culture, params object?[] args)
        {
            string formatString = GetString(resourceKey);
            return string.IsNullOrEmpty(formatString)
                ? string.Empty
                : string.Format(culture, formatString, args);
        }
    }
}
