// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace ColorPicker.Mouse
{
    internal interface IScreenColorSampler
    {
        bool TrySample(out ScreenColorSample sample, out ScreenColorSamplingFailure failure);
    }

    internal readonly struct ScreenColorSample
    {
        public ScreenColorSample(System.Windows.Point position, Color color)
        {
            Position = position;
            Color = color;
        }

        public System.Windows.Point Position { get; }

        public Color Color { get; }
    }

    internal enum ScreenColorSamplingFailureReason
    {
        None,
        CursorUnavailable,
        ScreenCaptureFailed,
    }

    internal readonly struct ScreenColorSamplingFailure
    {
        public ScreenColorSamplingFailure(ScreenColorSamplingFailureReason reason, int nativeErrorCode, string message)
        {
            Reason = reason;
            NativeErrorCode = nativeErrorCode;
            Message = message;
        }

        public ScreenColorSamplingFailureReason Reason { get; }

        public int NativeErrorCode { get; }

        public string Message { get; }
    }
}
