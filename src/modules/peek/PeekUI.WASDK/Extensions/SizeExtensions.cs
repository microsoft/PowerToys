// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PeekUI.WASDK.Extensions
{
    using System.Drawing;

    public static class SizeExtensions
    {
        public static Size Fit(this Size sizeToFit, Size maxSize, Size minSize)
        {
            double fittedWidth = sizeToFit.Width;
            double fittedHeight = sizeToFit.Height;

            double ratioWidth = (double)sizeToFit.Width / maxSize.Width;
            double ratioHeight = (double)sizeToFit.Height / maxSize.Height;

            if (ratioWidth > ratioHeight)
            {
                if (ratioWidth > 1)
                {
                    fittedWidth = maxSize.Width;
                    fittedHeight = sizeToFit.Height / ratioWidth;
                }
            }
            else
            {
                if (ratioHeight > 1)
                {
                    fittedWidth = sizeToFit.Width / ratioHeight;
                    fittedHeight = maxSize.Height;
                }
            }

            if (fittedWidth < minSize.Width)
            {
                fittedWidth = minSize.Width;
            }

            if (fittedHeight < minSize.Height)
            {
                fittedHeight = minSize.Height;
            }

            return new Size((int)fittedWidth, (int)fittedHeight);
        }
    }
}
