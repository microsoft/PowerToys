// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace MouseJump.Common.Blurring;

/// <summary>
/// Represents the state of the blurred screenshot images for one physical screen
/// within a <see cref="ScreenshotBlurPipeline"/> - see its remarks for what
/// "todo" / "doing" (<see cref="BlurInProgress"/>) / "done" mean.
/// </summary>
internal sealed class ScreenshotBlurState
{
    public bool BlurInProgress
    {
        get;
        set;
    }

    public Bitmap? Todo
    {
        get;
        set;
    }

    public Bitmap? Done
    {
        get;
        set;
    }

    public DateTimeOffset DoneAt
    {
        get;
        set;
    }
}
