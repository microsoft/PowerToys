// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// Makes a closed ComboBox draw the current text of its selected item. The box keeps the text
    /// the item had when it was selected, so the bound index reads -1 for one change notice and
    /// then its value again. While that runs, the index's setter ignores what the box writes back.
    /// </summary>
    public sealed class CursorBrakeReselect
    {
        /// <summary>Gets a value indicating whether the bound index reads -1.</summary>
        public bool IsActive { get; private set; }

        /// <param name="notify">Raises the change notice for the bound index.</param>
        public void Run(Action notify)
        {
            ArgumentNullException.ThrowIfNull(notify);
            IsActive = true;
            try
            {
                notify();
            }
            finally
            {
                IsActive = false;
            }

            notify();
        }
    }
}
