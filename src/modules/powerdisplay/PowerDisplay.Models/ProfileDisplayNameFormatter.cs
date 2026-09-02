// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Text;

namespace PowerDisplay.Models
{
    internal static class ProfileDisplayNameFormatter
    {
        private const string NeutralFormat = "{0} (#{1})";
        private const string ResourceName = "ProfileDisplayNameFormat";
        private static readonly CompositeFormat NeutralCompositeFormat = CompositeFormat.Parse(NeutralFormat);
        private static readonly ConcurrentDictionary<string, CompositeFormat> ParsedFormats = new(StringComparer.Ordinal);

        private static readonly ResourceManager ResourceManager = new(
            "PowerDisplay.Models.Properties.Resources",
            typeof(ProfileDisplayNameFormatter).Assembly);

        public static string Format(string name, int id)
        {
            var format = ResourceManager.GetString(
                ResourceName,
                CultureInfo.CurrentUICulture);
            return Format(name, id, format);
        }

        internal static string Format(string name, int id, string? format)
        {
            try
            {
                var selectedFormat = string.IsNullOrEmpty(format)
                    ? NeutralCompositeFormat
                    : string.Equals(format, NeutralFormat, StringComparison.Ordinal)
                        ? NeutralCompositeFormat
                        : ParsedFormats.GetOrAdd(format, static value => CompositeFormat.Parse(value));

                return string.Format(
                    CultureInfo.CurrentCulture,
                    selectedFormat,
                    name,
                    id);
            }
            catch (FormatException ex)
            {
                Trace.TraceError(
                    $"Invalid {ResourceName} resource: {ex.Message}");
                return string.Format(
                    CultureInfo.CurrentCulture,
                    NeutralCompositeFormat,
                    name,
                    id);
            }
        }
    }
}
