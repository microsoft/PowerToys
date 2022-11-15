// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerAccent.Core;

public struct Size
{
    public Size()
    {
        Width = 0;
        Height = 0;
    }

    public Size(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public Size(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public double Width { get; init; }

    public double Height { get; init; }

    public static implicit operator Size(System.Drawing.Size size) => new Size(size.Width, size.Height);

    public static Size operator /(Size size, double divider)
    {
        if (divider == 0)
        {
            throw new DivideByZeroException();
        }

        return new Size(size.Width / divider, size.Height / divider);
    }

    public static Size operator /(Size size, Size divider)
    {
        if (divider.Width == 0 || divider.Height == 0 || divider.Width == 0 || divider.Height == 0)
        {
            throw new DivideByZeroException();
        }

        return new Size(size.Width / divider.Width, size.Height / divider.Height);
    }
}
