// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using static ColorPicker.NativeMethods;

namespace ColorPicker.Mouse
{
    internal sealed class ScreenColorSampler : IScreenColorSampler
    {
        public bool TryGetCursorPosition(out System.Windows.Point position, out ScreenColorSamplingFailure failure)
        {
            if (!GetCursorPos(out PointInter cursorPosition))
            {
                position = default;
                failure = new ScreenColorSamplingFailure(
                    ScreenColorSamplingFailureReason.CursorUnavailable,
                    Marshal.GetLastWin32Error(),
                    "GetCursorPos failed.");
                return false;
            }

            position = (System.Windows.Point)cursorPosition;
            failure = default;
            return true;
        }

        public bool TrySampleColor(System.Windows.Point position, out Color color, out ScreenColorSamplingFailure failure)
        {
            try
            {
                color = GetPixelColor(position);
                failure = default;
                return true;
            }
            catch (Win32Exception ex)
            {
                color = default;
                failure = new ScreenColorSamplingFailure(
                    ScreenColorSamplingFailureReason.ScreenCaptureFailed,
                    ex.NativeErrorCode,
                    ex.Message);
                return false;
            }
        }

        private static Color GetPixelColor(System.Windows.Point mousePosition)
        {
            var rect = new Rectangle((int)mousePosition.X, (int)mousePosition.Y, 1, 1);
            using (var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(bmp))
                {
                    graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
                }

                return bmp.GetPixel(0, 0);
            }
        }
    }
}
