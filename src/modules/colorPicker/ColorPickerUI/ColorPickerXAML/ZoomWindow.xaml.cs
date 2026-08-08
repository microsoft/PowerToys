// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Common.UI.Controls.Window;

namespace ColorPicker
{
    /// <summary>
    /// The zoom magnifier window: a transparent, frameless, no-activate
    /// <see cref="TransparentWindow"/> hosting the Win2D-backed <see cref="Views.ZoomView"/>.
    /// </summary>
    public sealed partial class ZoomWindow : TransparentWindow
    {
        public ZoomWindow()
        {
            InitializeComponent();
        }
    }
}
