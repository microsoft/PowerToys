// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

namespace PeekUI.Extensions
{
    public static class SizeExtensions
    {
        public static Rect Fit(this Size sizeToFit, Rect bounds, Size maxSize, Size minSize, Size allowedGap, double reservedHeight)
        {
            double resultingWidth = sizeToFit.Width;
            double resultingHeight = sizeToFit.Height;

            var ratioWidth = sizeToFit.Width / maxSize.Width;
            var ratioHeight = sizeToFit.Height / maxSize.Height;

            if (ratioWidth > ratioHeight)
            {
                if (ratioWidth > 1)
                {
                    resultingWidth = maxSize.Width;
                    resultingHeight = sizeToFit.Height / ratioWidth;
                }
            }
            else
            {
                if (ratioHeight > 1)
                {
                    resultingWidth = sizeToFit.Width / ratioHeight;
                    resultingHeight = maxSize.Height;
                }
            }

            if (resultingWidth < minSize.Width - allowedGap.Width)
            {
                resultingWidth = minSize.Width;
            }

            if (resultingHeight < minSize.Height - allowedGap.Height)
            {
                resultingHeight = minSize.Height;
            }

            resultingHeight += reservedHeight;

            // Calculate offsets to center content
            double offsetX = (maxSize.Width - resultingWidth) / 2;
            double offsetY = (maxSize.Height - resultingHeight) / 2;

            var maxWindowLeft = bounds.Left + ((bounds.Right - bounds.Left - maxSize.Width) / 2);
            var maxWindowTop = bounds.Top + ((bounds.Bottom - bounds.Top - maxSize.Height) / 2);

            var resultingLeft = maxWindowLeft + offsetX;
            var resultingTop = maxWindowTop + offsetY;

            return new Rect(resultingLeft, resultingTop, resultingWidth, resultingHeight);
        }
    }
}
