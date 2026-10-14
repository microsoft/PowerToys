// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJump.Models.ViewModel;

/// <summary>
/// Defines the preview form's layout.
/// </summary>
public sealed class FormViewModel
{
    public sealed class Builder
    {
        public CanvasViewModel.Builder? CanvasLayout
        {
            get;
            set;
        }

        public FormViewModel Build()
        {
            return new FormViewModel(
                canvasLayout: (this.CanvasLayout ?? throw new InvalidOperationException($"{nameof(this.CanvasLayout)} must be initialized before calling {nameof(this.Build)}."))
                    .Build());
        }
    }

    public FormViewModel(
        CanvasViewModel canvasLayout)
    {
        this.CanvasLayout = canvasLayout ?? throw new ArgumentNullException(nameof(canvasLayout));
    }

    public CanvasViewModel CanvasLayout
    {
        get;
    }
}
