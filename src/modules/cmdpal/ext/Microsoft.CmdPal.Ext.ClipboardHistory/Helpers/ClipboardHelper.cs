// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Html;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;

namespace Microsoft.CmdPal.Ext.ClipboardHistory;

internal static class ClipboardHelper
{
    private static readonly ushort[] PasteChord = [(ushort)VirtualKey.Control, (ushort)VirtualKey.V];

    private static readonly InputChordOptions PasteChordOptions = new()
    {
        // Any key event with the extraInfo set to this value will be ignored by the keyboard hook and sent to the system instead.
        ExtraInfo = (UIntPtr)0x5555,
    };

    private static readonly HashSet<string> ImageFileTypes = new(StringComparer.InvariantCultureIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".ico", ".svg" };

    private static readonly (string DataFormat, ClipboardFormat ClipboardFormat)[] DataFormats =
    [
        (StandardDataFormats.Text, ClipboardFormat.Text),
        (StandardDataFormats.Html, ClipboardFormat.Html),
        (StandardDataFormats.Bitmap, ClipboardFormat.Image),
    ];

    private static readonly ClipboardThreadQueue ClipboardThreadQueue = new ClipboardThreadQueue();

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
        if (!string.IsNullOrEmpty(text))
        {
            DataPackage output = new();
            output.SetText(text);
            try
            {
                ClipboardThreadQueue.EnqueueTask(() =>
                {
                    Clipboard.SetContent(output);

                    Flush();
                    ExtensionHost.LogMessage(new LogMessage() { Message = "Copied text to clipboard" });
                });
            }
            catch (COMException ex)
            {
                ExtensionHost.LogMessage($"Error: {ex.HResult}\n{ex.Source}\n{ex.StackTrace}");
            }
        }
    }

    private static bool Flush()
    {
        // TODO(stefan): For some reason Flush() fails from time to time when directly activated via hotkey.
        // Calling inside a loop makes it work.
        // Exception is: The operation is not permitted because the calling application is not the owner of the data on the clipboard.
        ClipboardThreadQueue.EnqueueTask(() =>
        {
            const int maxAttempts = 5;

            for (var i = 1; i <= maxAttempts; i++)
            {
                try
                {
                    Clipboard.Flush();
                    return;
                }
                catch (Exception ex)
                {
                    if (i == maxAttempts)
                    {
                        ExtensionHost.LogMessage(new LogMessage()
                        {
                            Message = $"{nameof(Clipboard)}.{nameof(Flush)}() failed: {ex}",
                        });
                    }
                }
            }
        });

        // We cannot get the real result of the Flush() call here, as it is executed in a different thread.
        return true;
    }

    private static async Task<bool> FlushAsync() => await Task.Run(Flush);

    internal static async Task SetClipboardFileContentAsync(string fileName)
    {
        var storageFile = await StorageFile.GetFileFromPathAsync(fileName);

        DataPackage output = new();
        output.SetStorageItems((StorageFile[])[storageFile]);
        ClipboardThreadQueue.EnqueueTask(() => Clipboard.SetContent(output));

        await FlushAsync();
    }

    internal static void SetClipboardImageContent(RandomAccessStreamReference image)
    {
        ExtensionHost.LogMessage(new LogMessage() { Message = "Copied image to clipboard" });

        if (image is not null)
        {
            DataPackage output = new();
            output.SetBitmap(image);
            ClipboardThreadQueue.EnqueueTask(() => Clipboard.SetContentWithOptions(output, null));

            Flush();
        }
    }

    internal static void SetClipboardContent(ClipboardItem clipboardItem, ClipboardFormat clipboardFormat)
    {
        switch (clipboardFormat)
        {
            case ClipboardFormat.Text:
                if (clipboardItem.Content is null)
                {
                    ExtensionHost.LogMessage(new LogMessage() { Message = "No valid clipboard content" });
                    return;
                }
                else
                {
                    SetClipboardTextContent(clipboardItem.Content);
                }

                break;

            case ClipboardFormat.Image:
                if (clipboardItem.ImageData is null)
                {
                    ExtensionHost.LogMessage(new LogMessage() { Message = "No valid clipboard content" });
                    return;
                }
                else
                {
                    SetClipboardImageContent(clipboardItem.ImageData);
                }

                break;

            default:
                ExtensionHost.LogMessage(new LogMessage { Message = "Unsupported clipboard format." });
                break;
        }
    }

    internal static void SendPasteKeyCombination()
    {
        ExtensionHost.LogMessage(new LogMessage() { Message = "Sending paste keys..." });

        // Releases any modifier that is still held and sends Ctrl+V, all in one batch.
        var result = InputInjection.SendChord(PasteChord, PasteChordOptions);
        if (!result.Succeeded)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = $"Paste input was not fully sent: sent {result.Sent} of {result.Requested} events, error {result.Error}, cleanup sent {result.CleanupSent} of {result.CleanupRequested}", State = MessageState.Error });
            return;
        }

        ExtensionHost.LogMessage(new LogMessage() { Message = "Paste sent" });
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

    internal static async Task<SoftwareBitmap?> GetClipboardImageContentAsync(DataPackageView clipboardData)
    {
        using var stream = await GetClipboardImageStreamAsync(clipboardData);
        if (stream is not null)
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            return await decoder.GetSoftwareBitmapAsync();
        }

        return null;
    }

    private static async Task<IRandomAccessStream?> GetClipboardImageStreamAsync(DataPackageView clipboardData)
    {
        if (clipboardData.Contains(StandardDataFormats.StorageItems))
        {
            var storageItems = await clipboardData.GetStorageItemsAsync();
            var file = storageItems.Count == 1 ? storageItems[0] as StorageFile : null;
            if (file is not null)
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
