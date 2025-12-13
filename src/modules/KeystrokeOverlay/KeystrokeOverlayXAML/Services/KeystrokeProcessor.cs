// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using KeystrokeOverlayUI.Controls;
using KeystrokeOverlayUI.Models;

namespace KeystrokeOverlayUI.Services
{
    public class KeystrokeProcessor
    {
        private string _streamBuffer = string.Empty;

        /// <summary>
        /// Determines what visual action to take based on the incoming key and current mode.
        /// </summary>
        public KeystrokeResult Process(KeystrokeEvent kEvent, DisplayMode displayMode)
        {
            string formattedText = kEvent.ToString();

            // Early out for no text
            if (string.IsNullOrEmpty(formattedText))
            {
                return new KeystrokeResult { Action = KeystrokeAction.None };
            }

            bool isShortcut = kEvent.IsShortcut;

            // Mode-specific logic
            switch (displayMode)
            {
                case DisplayMode.Last5:
                    return new KeystrokeResult { Action = KeystrokeAction.Add, Text = formattedText };

                case DisplayMode.SingleCharactersOnly:
                    if (isShortcut)
                    {
                        return new KeystrokeResult { Action = KeystrokeAction.None };
                    }

                    return new KeystrokeResult { Action = KeystrokeAction.Add, Text = formattedText };

                case DisplayMode.ShortcutsOnly:
                    if (!isShortcut)
                    {
                        return new KeystrokeResult { Action = KeystrokeAction.None };
                    }

                    return new KeystrokeResult { Action = KeystrokeAction.Add, Text = formattedText };

                case DisplayMode.Stream:
                    return ProcessStreamMode(kEvent, isShortcut, formattedText);

                default:
                    return new KeystrokeResult { Action = KeystrokeAction.None };
            }
        }

        private KeystrokeResult ProcessStreamMode(KeystrokeEvent kEvent, bool isShortcut, string formattedText)
        {
            // handle Backspace
            if (kEvent.VirtualKey == (uint)Windows.System.VirtualKey.Back)
            {
                if (_streamBuffer.Length > 0)
                {
                    _streamBuffer = _streamBuffer.Substring(0, _streamBuffer.Length - 1);

                    if (string.IsNullOrEmpty(_streamBuffer))
                    {
                        return new KeystrokeResult { Action = KeystrokeAction.RemoveLast };
                    }
                    else
                    {
                        return new KeystrokeResult { Action = KeystrokeAction.ReplaceLast, Text = _streamBuffer };
                    }
                }

                return new KeystrokeResult { Action = KeystrokeAction.None };
            }

            // If a shortcut is pressed (and it's not Space), we reset the stream.
            if (isShortcut && kEvent.VirtualKey != (uint)Windows.System.VirtualKey.Space)
            {
                ResetBuffer();
                return new KeystrokeResult { Action = KeystrokeAction.Add, Text = formattedText };
            }

            // If it's a space/tab, we clear the buffer so the next character starts a new bubble.
            if (string.IsNullOrWhiteSpace(kEvent.Text))
            {
                ResetBuffer();
                return new KeystrokeResult { Action = KeystrokeAction.None };
            }

            _streamBuffer += kEvent.Text;

            // If this is the start of a word (Length 1), Add it.
            // If we are appending to a word (Length > 1), Replace the last bubble.
            if (_streamBuffer.Length == 1)
            {
                return new KeystrokeResult { Action = KeystrokeAction.Add, Text = _streamBuffer };
            }
            else
            {
                return new KeystrokeResult { Action = KeystrokeAction.ReplaceLast, Text = _streamBuffer };
            }
        }

        public void ResetBuffer()
        {
            _streamBuffer = string.Empty;
        }
    }
}
