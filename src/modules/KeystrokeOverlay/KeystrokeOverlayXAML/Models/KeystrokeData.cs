// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace KeystrokeOverlayUI.Models
{
    public class KeystrokeData
    {
        public string T { get; set; } // "down", "up", "char"

        public int VK { get; set; }

        public string Text { get; set; }

        public string[] Mods { get; set; }

        public double TS { get; set; }
    }
}
