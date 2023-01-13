// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                if (metadata.ContainsQuery(query))
                {
                    return metadata.GetQuery(query);
                }
                else
                {
                    return null;
                }
            }
            catch (NotSupportedException)
            {
                // NotSupportedException is throw if the metadata entry is not preset on the target image (e.g. Orientation not set).
                return null;
            }
        }

        public static void RemoveQuerySafe(this BitmapMetadata metadata, string query)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            try
            {
                if (metadata.ContainsQuery(query))
                {
                    metadata.RemoveQuery(query);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception while trying to remove metadata entry at position: {query}");
                Debug.WriteLine(ex);
            }
        }

        public static void SetQuerySafe(this BitmapMetadata metadata, string query, object value)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(query) || value == null)
            {
                return;
            }

            try
            {
                metadata.SetQuery(query, value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception while trying to set metadata {value} at position: {query}");
                Debug.WriteLine(ex);
            }
        }

        /// <summary>
        /// Gets all metadata.
        /// Iterates recursively through metadata and adds valid items to a list while skipping invalid data items.
        /// </summary>
        /// <remarks>
        /// Invalid data items are items which throw an exception when reading the data with metadata.GetQuery(...).
        /// Sometimes Metadata collections are improper closed and cause an exception on IEnumerator.MoveNext(). In this case, we return all data items which were successfully collected so far.
        /// </remarks>
        /// <returns>
        /// metadata path and metadata value of all successfully read data items.
        /// </returns>
        public static List<(string MetadataPath, object Value)> GetListOfMetadata(this BitmapMetadata metadata)
        {
            var listOfAllMetadata = new List<(string MetadataPath, object Value)>();

            try
            {
                GetMetadataRecursively(metadata, string.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception while trying to iterate recursively over metadata. We were able to read {listOfAllMetadata.Count} metadata entries.");
                Debug.WriteLine(ex);
            }

            return listOfAllMetadata;

            void GetMetadataRecursively(BitmapMetadata metadata, string query)
            {
                foreach (string relativeQuery in metadata)
                {
                    string absolutePath = query + relativeQuery;

                    object metadataQueryReader = null;

                    try
                    {
                        metadataQueryReader = GetQueryWithPreCheck(metadata, relativeQuery);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Removing corrupt metadata property {absolutePath}. Skipping metadata entry | {ex.Message}");
                        Debug.WriteLine(ex);
                    }

                    if (metadataQueryReader != null)
                    {
                        listOfAllMetadata.Add((absolutePath, metadataQueryReader));
                    }
                    else
                    {
                        Debug.WriteLine($"No metadata found for query {absolutePath}. Skipping empty null entry because its invalid.");
                    }

                    if (metadataQueryReader is BitmapMetadata innerMetadata)
                    {
                        GetMetadataRecursively(innerMetadata, absolutePath);
                    }
                }
            }

            object GetQueryWithPreCheck(BitmapMetadata metadata, string query)
            {
                if (metadata == null || string.IsNullOrWhiteSpace(query))
                {
                    return null;
                }

                if (metadata.ContainsQuery(query))
                {
                    return metadata.GetQuery(query);
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Prints all metadata to debug console
        /// </summary>
        /// <remarks>
        /// Intented for debug only!!!
        /// </remarks>
        public static void PrintsAllMetadataToDebugOutput(this BitmapMetadata metadata)
        {
            if (metadata == null)
            {
                Debug.WriteLine($"Metadata was null.");
            }

            var listOfMetadata = metadata.GetListOfMetadataForDebug();
            foreach (var metadataItem in listOfMetadata)
            {
                // Debug.WriteLine($"modifiableMetadata.RemoveQuerySafe(\"{metadataItem.metadataPath}\");");
                Debug.WriteLine($"{metadataItem.MetadataPath} | {metadataItem.Value}");
            }
        }

        /// <summary>
        /// Gets all metadata
        /// Iterates recursively through all metadata
        /// </summary>
        /// <remarks>
        /// Intented for debug only!!!
        /// </remarks>
        public static List<(string MetadataPath, object Value)> GetListOfMetadataForDebug(this BitmapMetadata metadata)
        {
            var listOfAllMetadata = new List<(string MetadataPath, object Value)>();

            try
            {
                GetMetadataRecursively(metadata, string.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception while trying to iterate recursively over metadata. We were able to read {listOfAllMetadata.Count} metadata entries.");
                Debug.WriteLine(ex);
            }

            return listOfAllMetadata;

            void GetMetadataRecursively(BitmapMetadata metadata, string query)
            {
                if (metadata == null)
                {
                    return;
                }

                foreach (string relativeQuery in metadata)
                {
                    string absolutePath = query + relativeQuery;

                    object metadataQueryReader = null;

                    try
                    {
                        metadataQueryReader = metadata.GetQuerySafe(relativeQuery);
                        listOfAllMetadata.Add((absolutePath, metadataQueryReader));
                    }
                    catch (Exception ex)
                    {
                        listOfAllMetadata.Add((absolutePath, $"######## INVALID METADATA: {ex.Message}"));
                        Debug.WriteLine(ex);
                    }

                    if (metadataQueryReader is BitmapMetadata innerMetadata)
                    {
                        GetMetadataRecursively(innerMetadata, absolutePath);
                    }
                }
            }
        }
    }
}
