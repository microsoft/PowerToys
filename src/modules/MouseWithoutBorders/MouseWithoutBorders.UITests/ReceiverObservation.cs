// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;

namespace Microsoft.MouseWithoutBorders.UITests;

internal static class ReceiverObservation
{
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
