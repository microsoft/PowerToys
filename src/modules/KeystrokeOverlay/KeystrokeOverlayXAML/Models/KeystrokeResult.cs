// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeystrokeOverlayUI.Models
{
    public enum KeystrokeAction
    {
        None,           // Do nothing
        Add,            // Create a new visual bubble (e.g., new word or shortcut)
        ReplaceLast,    // Update the current bubble (e.g., typing "Hell" -> "Hello")
        RemoveLast,      // Backspace a full bubble
    }

    public struct KeystrokeResult
    {
        public KeystrokeAction Action { get; set; }

        public string Text { get; set; }
    }
}
