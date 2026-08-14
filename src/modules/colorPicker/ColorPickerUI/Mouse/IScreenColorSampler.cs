// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace ColorPicker.Mouse
{
    internal interface IScreenColorSampler
    {
        bool TryGetCursorPosition(out System.Windows.Point position, out ScreenColorSamplingFailure failure);

        bool TrySampleColor(System.Windows.Point position, out Color color, out ScreenColorSamplingFailure failure);
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
