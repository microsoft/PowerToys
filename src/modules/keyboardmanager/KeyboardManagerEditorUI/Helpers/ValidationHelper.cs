// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyboardManagerEditorUI.Helpers
{
    public static class ValidationHelper
    {
        public enum ValidationErrorType
        {
            EmptyOriginalKeys,
            EmptyRemappedKeys,
            ModifierOnly,
            EmptyAppName,
            IllegalShortcut,
            DuplicateMapping,
            SelfMapping,
        }

        public static readonly Dictionary<ValidationErrorType, (string Title, string Message)> ValidationMessages = new()
        {
            { ValidationErrorType.EmptyOriginalKeys, ("Missing Original Keys", "Please enter at least one original key to create a remapping.") },
            { ValidationErrorType.EmptyRemappedKeys, ("Missing Target Keys", "Please enter at least one target key to create a remapping.") },
            { ValidationErrorType.ModifierOnly, ("Invalid Shortcut", "Shortcuts must contain at least one action key in addition to modifier keys (Ctrl, Alt, Shift, Win).") },
            { ValidationErrorType.EmptyAppName, ("Missing Application Name", "You've selected app-specific remapping but haven't specified an application name. Please enter the application name.") },
            { ValidationErrorType.IllegalShortcut, ("Reserved System Shortcut", "Win+L and Ctrl+Alt+Delete are reserved system shortcuts and cannot be remapped.") },
            { ValidationErrorType.DuplicateMapping, ("Duplicate Remapping", "This key or shortcut is already remapped.") },
            { ValidationErrorType.SelfMapping, ("Invalid Remapping", "A key or shortcut cannot be remapped to itself. Please choose a different target.") },
        };
    }
}
