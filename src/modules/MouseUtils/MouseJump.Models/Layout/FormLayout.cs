// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using MouseJump.Models.Drawing;

namespace MouseJump.Models.Layout;

/// <summary>
/// Defines the preview form's layout.
/// </summary>
public sealed class FormLayout
{
    public sealed class Builder
    {
        public CanvasLayout.Builder? CanvasLayout
        {
            get;
            set;
        }

        public FormLayout Build()
        {
            return new FormLayout(
                canvasLayout: (this.CanvasLayout ?? throw new InvalidOperationException($"{nameof(this.CanvasLayout)} must be initialized before calling {nameof(this.Build)}."))
                    .Build());
        }
    }

    public FormLayout(
        CanvasLayout canvasLayout)
    {
        this.CanvasLayout = canvasLayout ?? throw new ArgumentNullException(nameof(canvasLayout));
    }

    public CanvasLayout CanvasLayout
    {
        get;
    }
}
