// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerAccent.Core;

public struct Size
{
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
}
