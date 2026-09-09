// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Grid navigation geometry independent of realized UI containers. Only one
/// record per nonempty group is needed, including groups far outside the viewport.
/// </summary>
public sealed class GridNavigationLayout
{
    private readonly List<GroupLayout> _groups = [];
    private readonly int _columns;
    private readonly double _itemHeight;
    private readonly int _itemCount;

    public GridNavigationLayout(IReadOnlyList<GridItemGroupViewModel> groups, int columns, double itemHeight, double headerHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
        if (!double.IsFinite(itemHeight) || itemHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemHeight));
        }

        if (!double.IsFinite(headerHeight) || headerHeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(headerHeight));
        }

        _columns = columns;
        _itemHeight = itemHeight;
        var top = 0.0;
        foreach (var group in groups)
        {
            var itemCount = group.Items.Count;
            if (group.HasHeader)
            {
                top += headerHeight;
            }

            if (itemCount == 0)
            {
                continue;
            }

            var rows = 1 + ((itemCount - 1) / columns);
            _groups.Add(new GroupLayout(_itemCount, itemCount, rows, top));
            _itemCount += itemCount;
            top += rows * itemHeight;
        }
    }

    public int GetColumn(int index)
    {
        var group = FindGroup(index);
        return group < 0 ? 0 : (index - _groups[group].FirstIndex) % _columns;
    }

    public static int MoveHorizontal(int index, bool increaseIndex, int itemCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(itemCount);
        if (itemCount == 0)
        {
            return -1;
        }

        if (index < 0 || index >= itemCount)
        {
            return 0;
        }

        return increaseIndex
            ? index == itemCount - 1 ? 0 : index + 1
            : index == 0 ? itemCount - 1 : index - 1;
    }

    public int MoveVertical(int index, bool down, int column, bool wrap)
    {
        var groupIndex = FindGroup(index);
        if (groupIndex < 0)
        {
            return _itemCount == 0 ? -1 : 0;
        }

        var group = _groups[groupIndex];
        var row = ((index - group.FirstIndex) / _columns) + (down ? 1 : -1);
        if (row < 0 || row >= group.Rows)
        {
            groupIndex += down ? 1 : -1;
            if (groupIndex < 0 || groupIndex >= _groups.Count)
            {
                if (!wrap)
                {
                    return index;
                }

                groupIndex = down ? 0 : _groups.Count - 1;
            }

            group = _groups[groupIndex];
            row = down ? 0 : group.Rows - 1;
        }

        return IndexAt(group, row, column);
    }

    public int MovePage(int index, bool down, int column, double viewportHeight)
    {
        if (!double.IsFinite(viewportHeight) || viewportHeight <= 0)
        {
            return index;
        }

        var groupIndex = FindGroup(index);
        if (groupIndex < 0)
        {
            return _itemCount == 0 ? -1 : 0;
        }

        var current = _groups[groupIndex];
        var currentRow = (index - current.FirstIndex) / _columns;
        var targetTop = current.Top + (currentRow * _itemHeight) + (down ? viewportHeight : -viewportHeight);

        // Find the last group whose tiles start at or before the target offset.
        var low = 0;
        var high = _groups.Count;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (_groups[middle].Top <= targetTop)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        groupIndex = Math.Max(0, low - 1);
        var targetGroup = _groups[groupIndex];
        if (down && targetTop >= targetGroup.Top + (targetGroup.Rows * _itemHeight) && groupIndex + 1 < _groups.Count)
        {
            // The target falls in a header (possibly several empty groups).
            targetGroup = _groups[groupIndex + 1];
        }

        var row = (int)Math.Clamp(Math.Floor((targetTop - targetGroup.Top) / _itemHeight), 0, targetGroup.Rows - 1);
        var target = IndexAt(targetGroup, row, column);
        return target == index ? MoveVertical(index, down, column, wrap: false) : target;
    }

    private int IndexAt(GroupLayout group, int row, int column)
        => group.FirstIndex + Math.Min((row * _columns) + Math.Clamp(column, 0, _columns - 1), group.Count - 1);

    private int FindGroup(int index)
    {
        if (index < 0 || index >= _itemCount)
        {
            return -1;
        }

        var low = 0;
        var high = _groups.Count;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (_groups[middle].FirstIndex <= index)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low - 1;
    }

    private readonly record struct GroupLayout(int FirstIndex, int Count, int Rows, double Top);
}
