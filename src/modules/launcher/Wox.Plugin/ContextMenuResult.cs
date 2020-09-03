// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Input;

namespace Wox.Plugin
{
    public class ContextMenuResult
    {
        public string PluginName { get; set; }

        public string Title { get; set; }

        public string Glyph { get; set; }

        public string FontFamily { get; set; }

        public Key AcceleratorKey { get; set; }

        public ModifierKeys AcceleratorModifiers { get; set; }

        /// <summary>
        /// Gets or sets return true to hide wox after select result
        /// </summary>
        public Func<ActionContext, bool> Action { get; set; }

        public override string ToString()
        {
            return Title;
        }
    }
}
