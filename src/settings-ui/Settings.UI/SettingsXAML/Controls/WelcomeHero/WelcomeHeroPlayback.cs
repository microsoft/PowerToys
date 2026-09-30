// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    /// <summary>
    /// How the Welcome hero enters the screen.
    /// </summary>
    public enum WelcomeHeroPlayback
    {
        /// <summary>
        /// The full "Warp" intro, played on the first visit of the Welcome page.
        /// </summary>
        Intro,

        /// <summary>
        /// A short fade and scale, used when the Welcome page is visited again.
        /// </summary>
        Settle,

        /// <summary>
        /// The final state without motion, used when Windows animations are turned off.
        /// </summary>
        Static,
    }
}
