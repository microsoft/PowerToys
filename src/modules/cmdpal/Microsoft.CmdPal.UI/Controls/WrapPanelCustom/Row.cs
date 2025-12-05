// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls;

internal sealed class Row
{
    public List<UvRect> ChildrenRects { get; } = new();

    public UvMeasure Size { get; set; }

    public UvRect Rect
    {
        get
        {
            UvRect result;
            if (ChildrenRects.Count <= 0)
            {
                result = new UvRect();
                result.Position = UvMeasure.Zero;
                result.Size = Size;
                return result;
            }

            result = new UvRect();
            result.Position = ChildrenRects.First().Position;
            result.Size = Size;
            return result;
        }
    }

    public Row(List<UvRect> childrenRects, UvMeasure size)
    {
        ChildrenRects = childrenRects;
        Size = size;
    }

    public void Add(UvMeasure position, UvMeasure size)
    {
        ChildrenRects.Add(new UvRect
        {
            Position = position,
            Size = size,
        });

        Size = new UvMeasure
        {
            U = position.U + size.U,
            V = Math.Max(Size.V, size.V),
        };
    }
}
