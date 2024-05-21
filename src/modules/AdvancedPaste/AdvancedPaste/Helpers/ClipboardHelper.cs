// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Specialized;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;

namespace AdvancedPaste.Helpers
{
    internal static class ClipboardHelper
    {
        public enum ClipboardContentFormats
        {
            Text,
            Image,
            File,
            HTML,
            Audio,
            Invalid,
        }

        internal static void SetClipboardTextContent(string text)
        {
            Logger.LogTrace();

            if (!string.IsNullOrEmpty(text))
            {
                DataPackage output = new();
                output.SetText(text);
                Clipboard.SetContentWithOptions(output, null);

                // TODO(stefan): For some reason Flush() fails from time to time when directly activated via hotkey.
                // Calling inside a loop makes it work.
                bool flushed = false;
                for (int i = 0; i < 5; i++)
                {
                    if (flushed)
                    {
                        break;
                    }

                    try
                    {
                        Task.Run(() =>
                        {
                            Clipboard.Flush();
                        }).Wait();

                        flushed = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Clipboard.Flush() failed", ex);
                    }
                }
            }
        }

        internal static void SetClipboardImageContent(RandomAccessStreamReference image)
        {
            Logger.LogTrace();

            if (image is not null)
            {
                DataPackage output = new();
                output.SetBitmap(image);
                Clipboard.SetContentWithOptions(output, null);

                // TODO(stefan): For some reason Flush() fails from time to time when directly activated via hotkey.
                // Calling inside a loop makes it work.
                bool flushed = false;
                for (int i = 0; i < 5; i++)
                {
                    if (flushed)
                    {
                        break;
                    }

                    try
                    {
                        Task.Run(() =>
                        {
                            Clipboard.Flush();
                        }).Wait();

                        flushed = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Clipboard.Flush() failed", ex);
                    }
                }
            }
        }

        internal static string ConvertHTMLToPlainText(string inputHTML)
        {
            return System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(inputHTML, "<.*?>", string.Empty));
        }

        internal static async Task<bool> SetClipboardFile(string fileName)
        {
            Logger.LogTrace();

            if (fileName != null)
            {
                StorageFile storageFile = await StorageFile.GetFileFromPathAsync(fileName).AsTask();

                DataPackage output = new();
                output.SetStorageItems(new[] { storageFile });
                Clipboard.SetContent(output);

                // TODO(stefan): For some reason Flush() fails from time to time when directly activated via hotkey.
                // Calling inside a loop makes it work.
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        Clipboard.Flush();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Clipboard.Flush() failed", ex);
                    }
                }
            }
            else
            {
                return false;
            }

            return true;
        }

        internal static void SetClipboardHTMLContent(string htmlContent)
        {
            Logger.LogTrace();

            if (htmlContent != null)
            {
                // Set htmlContent to output
                DataPackage output = new();
                output.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(htmlContent));

                // Extract plain text from HTML
                string plainText = ConvertHTMLToPlainText(htmlContent);

                output.SetText(plainText);
                Clipboard.SetContent(output);

                // TODO(stefan): For some reason Flush() fails from time to time when directly activated via hotkey.
                // Calling inside a loop makes it work.
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        Clipboard.Flush();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Clipboard.Flush() failed", ex);
                    }
                }
            }
            else
            {
                Console.WriteLine("Error");
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

        internal static async Task<string> GetClipboardTextContent(DataPackageView clipboardData)
        {
            if (clipboardData != null)
            {
                if (clipboardData.Contains(StandardDataFormats.Text))
                {
                    return await Task.Run(async () =>
                    {
                        string plainText = await clipboardData.GetTextAsync() as string;
                        return plainText;
                    });
                }
            }

            return string.Empty;
        }

        internal static async Task<string> GetClipboardHTMLContent(DataPackageView clipboardData)
        {
            if (clipboardData != null)
            {
                if (clipboardData.Contains(StandardDataFormats.Html))
                {
                    return await Task.Run(async () =>
                    {
                        string htmlText = await clipboardData.GetHtmlFormatAsync() as string;
                        return htmlText;
                    });
                }
            }

            return string.Empty;
        }

        internal static async Task<string> GetClipboardFileName(DataPackageView clipboardData)
        {
            if (clipboardData != null)
            {
                if (clipboardData.Contains(StandardDataFormats.StorageItems))
                {
                    return await Task.Run(async () =>
                    {
                        var storageItems = await clipboardData.GetStorageItemsAsync();
                        var file = storageItems[0] as StorageFile;
                        return file.Path;
                    });
                }
            }

            return string.Empty;
        }

        internal static async Task<SoftwareBitmap> GetClipboardImageContent(DataPackageView clipboardData)
        {
            SoftwareBitmap softwareBitmap = null;

            // Check if the clipboard contains a file reference
            if (clipboardData.Contains(StandardDataFormats.StorageItems))
            {
                var storageItems = await clipboardData.GetStorageItemsAsync();
                var file = storageItems[0] as StorageFile;
                if (file != null)
                {
                    using (var stream = await file.OpenReadAsync())
                    {
                        // Get image stream and create a software bitmap
                        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                        softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                    }
                }
            }
            else
            {
                if (clipboardData.Contains(StandardDataFormats.Bitmap))
                {
                    // If it's not a file reference, get bitmap directly
                    var imageStreamReference = await clipboardData.GetBitmapAsync();
                    var imageStream = await imageStreamReference.OpenReadAsync();
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(imageStream);
                    softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                }
            }

            return softwareBitmap;
        }
    }
}
