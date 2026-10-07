// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJump.Models.Layout;

/// <summary>
/// Defines the preview layout.
/// </summary>
public sealed class PreviewLayout
{
    public sealed class Builder
    {
        public CanvasLayout.Builder? CanvasLayout
        {
            get;
            set;
        }

        public PreviewLayout Build()
        {
            return new PreviewLayout(
                canvasLayout: (this.CanvasLayout ?? throw new InvalidOperationException($"{nameof(this.CanvasLayout)} must be initialized before calling {nameof(this.Build)}."))
                    .Build());
        }
    }

    public PreviewLayout(
        CanvasLayout canvasLayout)
    {
        this.CanvasLayout = canvasLayout ?? throw new ArgumentNullException(nameof(canvasLayout));
    }

    public CanvasLayout CanvasLayout
    {
        get;
    }
}
