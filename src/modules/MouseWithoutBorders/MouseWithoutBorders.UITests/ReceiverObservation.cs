// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;

namespace Microsoft.MouseWithoutBorders.UITests;

internal static class ReceiverObservation
{
    internal static (int X, int Y) ClickProbePosition(int left, int top, int right, int bottom, int probe)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(probe);
        if (right - left <= 62 || bottom - top <= 62)
        {
            throw new ArgumentException("The receiver has no safe physical click surface.");
        }

        var x = (probe % 4) is 0 or 2 ? left + ((right - left) / 4) : right - ((right - left) / 4);
        var y = (probe % 4) is 0 or 1 ? top + ((bottom - top) / 4) : bottom - ((bottom - top) / 4);
        return (Math.Clamp(x, left + 31, right - 31), Math.Clamp(y, top + 31, bottom - 31));
    }

    internal static bool IsPhysicalClickTarget(JsonObject observation, long expectedReceiver)
    {
        var target = observation["MouseTarget"] as JsonObject;
        long? panel = ReadHandle(observation, "ClickTargetHwnd");
        return expectedReceiver != 0 && panel is > 0 && target != null &&
            ReadHandle(observation, "ReceiverHwnd") == expectedReceiver &&
            ReadHandle(observation, "ForegroundHwnd") == expectedReceiver &&
            ReadHandle(target, "Window") == panel &&
            ReadHandle(target, "RootWindow") == expectedReceiver &&
            ReadHandle(target, "CaptureWindow") == 0;
    }

    private static long? ReadHandle(JsonObject observation, string name)
    {
        return observation[name] is JsonValue value && value.TryGetValue<long>(out long handle) ? handle : null;
    }
}
