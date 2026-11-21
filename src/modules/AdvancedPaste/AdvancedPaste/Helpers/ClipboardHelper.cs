// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.System;

namespace AdvancedPaste.Helpers;

internal static class ClipboardHelper
{
    private static readonly InputChordOptions PasteChordOptions = new()
    {
        // Any key event with the extra info set to this value is ignored by the keyboard hook and sent to the system instead.
        ExtraInfo = (UIntPtr)0x5555,

        // The user may still be holding the modifiers of the shortcut that triggered the paste.
        ModifierReleaseTimeout = TimeSpan.FromSeconds(1),
    };

    internal static async Task TryCopyPasteAsync(DataPackage dataPackage, Action onCopied)
    {
        Logger.LogTrace();

        bool hasUsableData = await dataPackage.GetView().HasUsableDataAsync();
        Logger.LogInfo($"Clipboard output validation completed: usable={hasUsableData}.");
        if (hasUsableData)
        {
            Clipboard.SetContent(dataPackage);
            Logger.LogInfo("Clipboard output assigned; flushing.");
            bool flushed = await FlushAsync();
            Logger.LogInfo($"Clipboard output flush completed: flushed={flushed}.");
            onCopied();

            // Waiting for the modifiers to be released blocks, so keep it off the UI thread.
            await Task.Run(SendPasteKeyCombination);
        }
    }

    internal static void SetTextContent(string text)
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

    internal static void SetImageContent(RandomAccessStreamReference image)
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

    private static bool Flush()
    {
        // TODO(stefan): For some reason Flush() fails from time to time when directly activated via hotkey.
        // Calling inside a loop makes it work.
        const int maxAttempts = 5;
        for (int i = 1; i <= maxAttempts; i++)
        {
            try
            {
                Clipboard.Flush();
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

    private static async Task<bool> FlushAsync()
    {
        // This should run on the UI thread to avoid the "calling application is not the owner of the data on the clipboard" error.
        return await Task.Factory.StartNew(Flush, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
    }

    internal static void SendPasteKeyCombination()
    {
        Logger.LogTrace();

        var result = InputInjection.SendChord(new[] { (ushort)VirtualKey.Control, (ushort)VirtualKey.V }, PasteChordOptions);
        if (result.ModifierWaitTimedOut)
        {
            Logger.LogWarning($"Modifier keys still held after {PasteChordOptions.ModifierReleaseTimeout.TotalMilliseconds}ms; released them before sending Ctrl+V");
        }

        if (!result.Succeeded)
        {
            Logger.LogError($"SendInput failed for Ctrl+V. Expected to send {result.Requested} inputs and sent only {result.Sent}; released {result.CleanupSent} of {result.CleanupRequested} pressed keys. Error {result.Error}");
            return;
        }

        Logger.LogInfo("Paste sent");
    }
}
