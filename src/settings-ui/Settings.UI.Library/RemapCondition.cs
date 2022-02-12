// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library.ViewModels
{
    /// <summary>
    /// The condition to apply a key remap.
    /// </summary>
    public enum RemapCondition
    {
        /// <summary>
        /// The remap is always effective.
        /// </summary>
        Always,

        /// <summary>
        /// The remap is effective only when the key is pressed alone.
        /// </summary>
        Alone,

        /// <summary>
        /// The remap is effective only when the key is pressed together with other keys.
        /// </summary>
        Combination,
    }
}
