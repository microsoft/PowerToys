// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;

namespace RunnerV2.Models
{
    internal interface IPowerToysModuleShortcutsProvider
    {
        /// <summary>
        /// Gets a list of shortcuts, that shall be registered in the keyboard hook, and their associated actions.
        /// </summary>
        /// <remarks>
        /// If this property is not overridden, the module is considered to not have shortcuts.
        /// </remarks>
        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; }
    }
}
