// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CropAndLock.UITests
{
    internal abstract class CropSource : IDisposable
    {
        internal IntPtr Window { get; private protected set; }

        internal Rectangle CropBounds { get; private protected set; }

        internal Rectangle InputBounds { get; private protected set; }

        internal abstract void Open(TestContext context);

        public abstract void Dispose();

        internal Rectangle ScreenCrop()
        {
            var crop = CropBounds;
            crop.Offset(NativeMethods.ClientBounds(Window).Location);
            return crop;
        }

        internal Point ScreenInput()
        {
            var client = NativeMethods.ClientBounds(Window);
            return new Point(client.Left + InputBounds.Left + (InputBounds.Width / 2), client.Top + InputBounds.Top + (InputBounds.Height / 2));
        }

        protected void SetGeometry(Rectangle cropBounds, Rectangle inputBounds)
        {
            InputBounds = inputBounds;
            CropBounds = cropBounds;
        }
    }
}
