// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// Identifies which Power Display dangerous-feature confirmation dialog to show.
    /// Each value maps to a pair of resw keys
    /// (<c>PowerDisplay_Warning_{Kind}_InfoBar</c> + <c>PowerDisplay_Warning_{Kind}_Body</c>)
    /// inside the shared <c>PowerDisplayWarningDialog</c> control.
    /// </summary>
    public enum PowerDisplayWarningKind
    {
        /// <summary>Shown when the user turns on the Power Display module itself.</summary>
        EnableModule,

        /// <summary>Shown when the user enables color temperature control for a monitor.</summary>
        ColorTemperature,

        /// <summary>Shown when the user enables power state control for a monitor.</summary>
        PowerState,

        /// <summary>Shown when the user enables input source control for a monitor.</summary>
        InputSource,

        /// <summary>Shown when the user enables maximum compatibility mode.</summary>
        MaxCompatibility,
    }
}
