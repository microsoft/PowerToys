// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using AdvancedPaste.Models;
using ManagedCommon;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Html;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;

namespace AdvancedPaste.Helpers
{
    internal static class ClipboardHelper
    {
        private static readonly HashSet<string> ImageFileTypes = new(StringComparer.InvariantCultureIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".ico", ".svg" };

        private static readonly (string DataFormat, ClipboardFormat ClipboardFormat)[] DataFormats =
        [
            (StandardDataFormats.Text, ClipboardFormat.Text),
            (StandardDataFormats.Html, ClipboardFormat.Html),
            (StandardDataFormats.Bitmap, ClipboardFormat.Image),
        ];

        internal static async Task<ClipboardFormat> GetAvailableClipboardFormatsAsync(DataPackageView clipboardData)
        {
            var availableClipboardFormats = DataFormats.Aggregate(
                ClipboardFormat.None,
                (result, formatPair) => clipboardData.Contains(formatPair.DataFormat) ? (result | formatPair.ClipboardFormat) : result);

            if (clipboardData.Contains(StandardDataFormats.StorageItems))
            {
                var storageItems = await clipboardData.GetStorageItemsAsync();

                if (storageItems.Count == 1 && storageItems.Single() is StorageFile file && ImageFileTypes.Contains(file.FileType))
                {
                    availableClipboardFormats |= ClipboardFormat.ImageFile;
                }
            }

            return availableClipboardFormats;
        }

        internal static void SetClipboardTextContent(string text)
        {
            Logger.LogTrace();

            if (!string.IsNullOrEmpty(text))
            {
                DataPackage output = new();
                output.SetText(text);
                Clipboard.SetContentWithOptions(output, null);

                Flush();
            }
        }

        private static bool Flush()
        {
            // TODO(stefan): For some reason Flush() fails from time to time when directly activated via hotkey.
            // Calling inside a loop makes it work.
            const int maxAttempts = 5;
            for (int i = 1; i <= maxAttempts; i++)
            {
                try
                {
                    Task.Run(Clipboard.Flush).Wait();
                    return true;
                }
                catch (Exception ex)
                {
                    if (i == maxAttempts)
                    {
                        Logger.LogError($"{nameof(Clipboard)}.{nameof(Flush)}() failed", ex);
                    }
                }
            }

            return false;
        }

        private static async Task<bool> FlushAsync() => await Task.Run(Flush);

        internal static async Task SetClipboardFileContentAsync(string fileName)
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(fileName);

            DataPackage output = new();
            output.SetStorageItems([storageFile]);
            Clipboard.SetContent(output);

            await FlushAsync();
        }

        internal static void SetClipboardImageContent(RandomAccessStreamReference image)
        {
            Logger.LogTrace();

            if (image is not null)
            {
                DataPackage output = new();
                output.SetBitmap(image);
                Clipboard.SetContentWithOptions(output, null);

                Flush();
            }
        }

        // Function to send a single key event
        private static void SendSingleKeyboardInput(short keyCode, uint keyStatus)
        {
            UIntPtr ignoreKeyEventFlag = (UIntPtr)0x5555;

            NativeMethods.INPUT inputShift = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUTTYPE.INPUT_KEYBOARD,
                data = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = keyCode,
                        dwFlags = keyStatus,

                        // Any keyevent with the extraInfo set to this value will be ignored by the keyboard hook and sent to the system instead.
                        dwExtraInfo = ignoreKeyEventFlag,
                    },
                },
            };

            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[] { inputShift };
            _ = NativeMethods.SendInput(1, inputs, NativeMethods.INPUT.Size);
        }

        internal static void SendPasteKeyCombination()
        {
            Logger.LogTrace();

            SendSingleKeyboardInput((short)VirtualKey.LeftControl, (uint)NativeMethods.KeyEventF.KeyUp);
            SendSingleKeyboardInput((short)VirtualKey.RightControl, (uint)NativeMethods.KeyEventF.KeyUp);
            SendSingleKeyboardInput((short)VirtualKey.LeftWindows, (uint)NativeMethods.KeyEventF.KeyUp);
            SendSingleKeyboardInput((short)VirtualKey.RightWindows, (uint)NativeMethods.KeyEventF.KeyUp);
            SendSingleKeyboardInput((short)VirtualKey.LeftShift, (uint)NativeMethods.KeyEventF.KeyUp);
            SendSingleKeyboardInput((short)VirtualKey.RightShift, (uint)NativeMethods.KeyEventF.KeyUp);
            SendSingleKeyboardInput((short)VirtualKey.LeftMenu, (uint)NativeMethods.KeyEventF.KeyUp);
            SendSingleKeyboardInput((short)VirtualKey.RightMenu, (uint)NativeMethods.KeyEventF.KeyUp);

            // Send Ctrl + V
            SendSingleKeyboardInput((short)VirtualKey.Control, (uint)NativeMethods.KeyEventF.KeyDown);
            SendSingleKeyboardInput((short)VirtualKey.V, (uint)NativeMethods.KeyEventF.KeyDown);
            SendSingleKeyboardInput((short)VirtualKey.V, (uint)NativeMethods.KeyEventF.KeyUp);
            SendSingleKeyboardInput((short)VirtualKey.Control, (uint)NativeMethods.KeyEventF.KeyUp);

            Logger.LogInfo("Paste sent");
        }

        internal static async Task<string> GetClipboardTextOrHtmlTextAsync(DataPackageView clipboardData)
        {
            if (clipboardData.Contains(StandardDataFormats.Text))
            {
                return await clipboardData.GetTextAsync();
            }
            else if (clipboardData.Contains(StandardDataFormats.Html))
            {
                var html = await clipboardData.GetHtmlFormatAsync();
                return HtmlUtilities.ConvertToText(html);
            }
            else
            {
                return string.Empty;
            }
        }

        internal static async Task<string> GetClipboardHtmlContentAsync(DataPackageView clipboardData) =>
            clipboardData.Contains(StandardDataFormats.Html) ? await clipboardData.GetHtmlFormatAsync() : string.Empty;

        internal static async Task<SoftwareBitmap> GetClipboardImageContentAsync(DataPackageView clipboardData)
        {
            using var stream = await GetClipboardImageStreamAsync(clipboardData);
            if (stream != null)
            {
                var decoder = await BitmapDecoder.CreateAsync(stream);
                return await decoder.GetSoftwareBitmapAsync();
            }

            return null;
        }

        private static async Task<IRandomAccessStream> GetClipboardImageStreamAsync(DataPackageView clipboardData)
        {
            if (clipboardData.Contains(StandardDataFormats.StorageItems))
            {
                var storageItems = await clipboardData.GetStorageItemsAsync();
                var file = storageItems.Count == 1 ? storageItems[0] as StorageFile : null;
                if (file != null)
                {
                    return await file.OpenReadAsync();
                }
            }

            if (clipboardData.Contains(StandardDataFormats.Bitmap))
            {
                var bitmap = await clipboardData.GetBitmapAsync();
                return await bitmap.OpenReadAsync();
            }

            return null;
        }
    }
}
