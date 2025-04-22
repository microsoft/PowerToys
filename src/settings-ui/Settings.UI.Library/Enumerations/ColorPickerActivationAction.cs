﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library.Enumerations
{
    public enum ColorPickerActivationAction
    {
        // Activation shortcut opens editor
        OpenEditor,

        // Activation shortcut opens color picker and after picking a color is copied into clipboard and editor optionally opens depending on which mouse button was pressed
        OpenColorPicker,
    }
}
