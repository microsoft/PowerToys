// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Wox.Infrastructure.Hotkey
{
    public enum KeyEvent
    {
        /// <summary>
        /// Key down
        /// </summary>
        WMKEYDOWN = 256,

        /// <summary>
        /// Key up
        /// </summary>
        WMKEYUP = 257,

        /// <summary>
        /// System key up
        /// </summary>
        WMSYSKEYUP = 261,

        /// <summary>
        /// System key down
        /// </summary>
        WMSYSKEYDOWN = 260,
    }
}
