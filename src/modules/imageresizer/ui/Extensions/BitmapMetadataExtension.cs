// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Media.Imaging;

namespace ImageResizer.Extensions
{
    internal static class BitmapMetadataExtension
    {
        public static void CopyMetadataPropertyTo(this BitmapMetadata source, BitmapMetadata target, string query)
        {
            if (source == null || target == null || string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            try
            {
                var value = source.GetQuerySafe(query);

                if (value == null)
                {
                    return;
                }

                target.SetQuery(query, value);
            }
            catch (InvalidOperationException)
            {
                // InvalidOperationException is thrown if metadata object is in readonly state.
                return;
            }
        }

        public static object GetQuerySafe(this BitmapMetadata metadata, string query)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            try
            {
                return metadata.GetQuery(query);
            }
            catch (NotSupportedException)
            {
                // NotSupportedException is throw if the metadata entry is not preset on the target image (e.g. Orientation not set).
                return null;
            }
        }
    }
}
