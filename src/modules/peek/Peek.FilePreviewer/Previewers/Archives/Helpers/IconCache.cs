// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.Common;
using Peek.Common.Models;
using Peek.FilePreviewer.Previewers.Helpers;

namespace Peek.FilePreviewer.Previewers.Archives.Helpers
{
    public class IconCache
    {
        private readonly Dictionary<string, BitmapSource> _cache = new();

        private BitmapSource? _directoryIconCache;

        public async Task<BitmapSource?> GetFileExtIconAsync(string fileName, CancellationToken cancellationToken)
        {
            var extension = Path.GetExtension(fileName);

            if (_cache.TryGetValue(extension, out var cachedIcon))
            {
                return cachedIcon;
            }

            try
            {
                var shFileInfo = default(SHFILEINFO);
                if (NativeMethods.SHGetFileInfo(fileName, NativeMethods.FILE_ATTRIBUTE_NORMAL, ref shFileInfo, (uint)Marshal.SizeOf(shFileInfo), NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_SMALLICON | NativeMethods.SHGFI_USEFILEATTRIBUTES) != IntPtr.Zero)
                {
                    var imageSource = await BitmapHelper.GetBitmapFromHIconAsync(shFileInfo.HIcon, cancellationToken);
                    _cache.Add(extension, imageSource);
                    return imageSource;
                }
                else
                {
                    Logger.LogError($"Icon extraction for extension {extension} failed with error {Marshal.GetLastWin32Error()}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Icon extraction for extension {extension} failed", ex);
            }

            return null;
        }

        public async Task<BitmapSource?> GetDirectoryIconAsync(CancellationToken cancellationToken)
        {
            if (_directoryIconCache != null)
            {
                return _directoryIconCache;
            }

            try
            {
                var shinfo = default(SHFILEINFO);
                if (NativeMethods.SHGetFileInfo("directory", NativeMethods.FILE_ATTRIBUTE_DIRECTORY, ref shinfo, (uint)Marshal.SizeOf(shinfo), NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_SMALLICON | NativeMethods.SHGFI_USEFILEATTRIBUTES) != IntPtr.Zero)
                {
                    var imageSource = await BitmapHelper.GetBitmapFromHIconAsync(shinfo.HIcon, cancellationToken);
                    _directoryIconCache = imageSource;
                    return imageSource;
                }
                else
                {
                    Logger.LogError($"Icon extraction for directory failed with error {Marshal.GetLastWin32Error()}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Icon extraction for directory failed", ex);
            }

            return null;
        }
    }
}
