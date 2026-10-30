// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseWithoutBorders.Core;

// Pure helper for the "reverse remote scroll direction" feature. Negates ONLY a vertical
// mouse-wheel delta, and only when reversing is enabled. The wheel-delta field is reused to
// carry XBUTTON identity and horizontal-wheel magnitude, so anything other than WM_MOUSEWHEEL
// must pass through unchanged. Keep this allocation-free and side-effect-free: it runs on the
// per-event mouse-forwarding path.
internal static class ScrollDirectionHelper
{
    internal static int ApplyReverse(int wheelDelta, bool reverse, int dwFlags)
    {
        return reverse && dwFlags == WM.WM_MOUSEWHEEL ? -wheelDelta : wheelDelta;
    }
}
