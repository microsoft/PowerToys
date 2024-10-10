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

namespace AdvancedPaste.Helpers
{
    internal static class ClipboardHelper
    {
        internal static void SetClipboardTextContent(string text)
        {
            Logger.LogTrace();

            if (!string.IsNullOrEmpty(text))
            {
                Logger.LogDebug("Text to set to clipboard: " + text);

                DataPackage output = new();
                output.SetText(text);
                bool success = Clipboard.SetContentWithOptions(output, null);
                Logger.LogDebug("Setting Clipboard data was success?: " + success);

                // Wait 50 ms in a loop until setting the clipboard content has really finished.
                while (!Clipboard.GetContent().GetTextAsync().GetAwaiter().GetResult().Contains(text))
                {
                    Thread.Sleep(50);
                }

#if DEBUG
                try
                {
                    string clipContent = Clipboard.GetContent().GetTextAsync().GetAwaiter().GetResult();
                    Logger.LogDebug("Clipboard content for current process: " + clipContent);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to get clipboard content: ", ex.GetBaseException());
                }
#endif

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
                            try
                            {
                                Clipboard.Flush();
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError("Clipboard.Flush() failed. Real reason:", ex.GetBaseException());
                                throw;
                            }
                        }).Wait();

                        flushed = true;
                        Logger.LogDebug("Clipboard flushed.");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Clipboard.Flush() failed", ex);
                        Thread.Sleep(50);
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
    }
}
