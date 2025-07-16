// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library.Enumerations
{
    public enum ColorPickerClickAction
    {
        // Clicking copies the picked color and opens the editor
        PickColorThenEditor,

        // Clicking only copies the picked color and then exits color picker
        PickColorAndClose,

        // Clicking exits color picker, without copying anything
        Close,
    }
}
