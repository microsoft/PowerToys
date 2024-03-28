// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Scripting;
using Windows.Foundation;
using Windows.Storage;

namespace Peek.Common.Extensions
{
    public static class IFileSystemItemExtensions
    {
        public static Size? GetImageSize(this IFileSystemItem item)
        {
            return PropertyStoreHelper.TryGetUintSizeProperty(item.Path, PropertyKey.ImageHorizontalSize, PropertyKey.ImageVerticalSize);
        }

        public static Size? GetVideoSize(this IFileSystemItem item)
        {
            return PropertyStoreHelper.TryGetUintSizeProperty(item.Path, PropertyKey.FrameWidth, PropertyKey.FrameHeight);
        }

        public static Size? GetSvgSize(this IFileSystemItem item)
        {
            Size? size = null;
            using (FileStream stream = new(item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.Async = true;
                settings.IgnoreComments = true;
                settings.IgnoreProcessingInstructions = true;
                settings.IgnoreWhitespace = true;

                using XmlReader reader = XmlReader.Create(stream, settings);
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "svg")
                    {
                        string? width = reader.GetAttribute("width");
                        string? height = reader.GetAttribute("height");
                        if (width != null && height != null)
                        {
                            int widthValue = int.Parse(Regex.Match(width, @"\d+").Value, NumberFormatInfo.InvariantInfo);
                            int heightValue = int.Parse(Regex.Match(height, @"\d+").Value, NumberFormatInfo.InvariantInfo);
                            size = new Size(widthValue, heightValue);
                        }
                        else
                        {
                            string? viewBox = reader.GetAttribute("viewBox");
                            if (viewBox != null)
                            {
                                var viewBoxValues = viewBox.Split(' ');
                                if (viewBoxValues.Length == 4)
                                {
                                    int viewBoxWidth = int.Parse(viewBoxValues[2], NumberStyles.Integer, CultureInfo.InvariantCulture);
                                    int viewBoxHeight = int.Parse(viewBoxValues[3], NumberStyles.Integer, CultureInfo.InvariantCulture);
                                    size = new Size(viewBoxWidth, viewBoxHeight);
                                }
                            }
                        }

                        reader.Close();
                    }
                }
            }

            return size;
        }

        public static Size? GetQoiSize(this IFileSystemItem item)
        {
            Size? size = null;
            using (FileStream stream = new(item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length >= 12)
                {
                    stream.Position = 4;

                    using var reader = new BinaryReader(stream);
                    uint widthValue = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));
                    uint heightValue = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));

                    if (widthValue > 0 && heightValue > 0)
                    {
                        size = new Size(widthValue, heightValue);
                    }
                }
            }

            return size;
        }

        public static async Task<string> GetContentTypeAsync(this IFileSystemItem item)
        {
            var storageItem = await item.GetStorageItemAsync();
            string contentType = storageItem switch
            {
                StorageFile storageFile => storageFile.DisplayType,
                StorageFolder storageFolder => storageFolder.DisplayType,
                _ => item.FileType,
            };
            return contentType;
        }
    }
}
