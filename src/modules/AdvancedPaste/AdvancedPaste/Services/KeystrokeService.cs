// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ManagedCommon;

namespace AdvancedPaste.Services;

/// <summary>
/// Service for sending text as keyboard input events.
/// </summary>
public sealed class KeystrokeService
{
    private const int WaitAfterSendInputEventsMS = 30;

    /// <summary>
    /// Sends text as individual Unicode keystroke events.
    /// This is useful for applications that don't support standard clipboard paste operations.
    /// </summary>
    /// <param name="text">The text to send as keystrokes.</param>
    /// <exception cref="ArgumentNullException">Thrown when text is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when SendInput fails to send all inputs.</exception>
    public void SendTextAsKeystrokes(string text)
    {
        Logger.LogTrace();

        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrEmpty(text))
        {
            Logger.LogWarning("Attempted to send empty text as keystrokes");
            return;
        }

        var targetWindow = Helpers.NativeMethods.GetForegroundWindow();

        const int chunkSize = 1;

        for (int i = 0; i < text.Length; i += chunkSize)
        {
            var currentForeground = Helpers.NativeMethods.GetForegroundWindow();
            if (targetWindow != IntPtr.Zero && currentForeground != targetWindow)
            {
                Logger.LogWarning("Keystroke paste cancelled because the foreground window changed");
                break;
            }

            int currentChunkLength = Math.Min(chunkSize, text.Length - i);
            string chunk = text.Substring(i, currentChunkLength);

            Logger.LogDebug(
                $"Sending keystroke chunk starting at index {i} with length {currentChunkLength}");

            if (chunk.Length > 0)
            {
                var inputs = CreateInputSequence(chunk);
                SendInputEvents(inputs);
            }
        }
    }

    private List<Helpers.NativeMethods.INPUT> CreateInputSequence(string text)
    {
        var inputs = new List<Helpers.NativeMethods.INPUT>(text.Length * 2);

        foreach (char c in text)
        {
            inputs.Add(CreateUnicodeInput(c, isKeyUp: false));

            inputs.Add(CreateUnicodeInput(c, isKeyUp: true));
        }

        return inputs;
    }

    private void SendInputEvents(List<Helpers.NativeMethods.INPUT> inputs)
    {
        uint sent = Helpers.NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), Helpers.NativeMethods.INPUT.Size);

        if (sent != inputs.Count)
        {
            var errorMessage = $"SendInput failed: only {sent} of {inputs.Count} inputs were sent";
            Logger.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        // SendInput cannot handle rapid sequences of inputs. 20ms causes scrambled and missing characters.
        System.Threading.Thread.Sleep(WaitAfterSendInputEventsMS);

        Logger.LogDebug($"Successfully sent {inputs.Count} keystrokes");
    }

    private static Helpers.NativeMethods.INPUT CreateUnicodeInput(char character, bool isKeyUp)
    {
        return new Helpers.NativeMethods.INPUT
        {
            type = Helpers.NativeMethods.INPUTTYPE.INPUT_KEYBOARD,
            data = new Helpers.NativeMethods.InputUnion
            {
                ki = new Helpers.NativeMethods.KEYBDINPUT
                {
                    wVk = 0,  // Must be 0 for Unicode input
                    wScan = (short)character,
                    dwFlags = (uint)Helpers.NativeMethods.KeyEventF.Unicode |
                              (isKeyUp ? (uint)Helpers.NativeMethods.KeyEventF.KeyUp : 0),
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero,
                },
            },
        };
    }
}
