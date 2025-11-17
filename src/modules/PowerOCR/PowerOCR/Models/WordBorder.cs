// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

namespace PowerOCR.Models;

public class WordBorder
{
    public bool IsSelected { get; set; }

    public string Word { get; set; } = string.Empty;

    public double Top { get; set; }

    public double Left { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public int LineNumber { get; set; }

    public double Right => Left + Width;

    public double Bottom => Top + Height;

    public int ResultRowID { get; set; }

    public int ResultColumnID { get; set; }

    public Rect AsRect()
    {
        return new Rect(Left, Top, Width, Height);
    }

    public bool IntersectsWith(Rect rect)
    {
        return rect.IntersectsWith(AsRect());
    }
}
