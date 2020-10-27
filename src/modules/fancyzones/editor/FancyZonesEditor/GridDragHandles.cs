// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    public class GridDragHandles
    {
        public GridDragHandles(UIElementCollection resizers, Action<object, DragDeltaEventArgs> dragDelta, Action<object, DragCompletedEventArgs> dragCompleted)
        {
            _resizers = resizers;
            _dragDelta = dragDelta;
            _dragCompleted = dragCompleted;
        }

        public void InitDragHandles(GridLayoutModel model)
        {
            if (_resizers.Count == 0)
            {
                int[,] indices = model.CellChildMap;

                // horizontal resizers
                for (int row = 0; row < model.Rows - 1; row++)
                {
                    for (int col = 0; col < model.Columns; col++)
                    {
                        if (indices[row, col] != indices[row + 1, col])
                        {
                            int endCol = col + 1;
                            while (endCol < model.Columns && indices[row, endCol] != indices[row + 1, endCol])
                            {
                                endCol++;
                            }

                            AddDragHandle(Orientation.Horizontal, row, row + 1, col, endCol, row);
                            col = endCol - 1;
                        }
                    }
                }

                // vertical resizers
                for (int col = 0; col < model.Columns - 1; col++)
                {
                    for (int row = 0; row < model.Rows; row++)
                    {
                        if (indices[row, col] != indices[row, col + 1])
                        {
                            int endRow = row + 1;
                            while (endRow < model.Rows && indices[endRow, col] != indices[endRow, col + 1])
                            {
                                endRow++;
                            }

                            AddDragHandle(Orientation.Vertical, row, endRow, col, col + 1, col + model.Rows - 1);
                            row = endRow - 1;
                        }
                    }
                }
            }
        }

        public void AddDragHandle(Orientation orientation, int foundRow, int foundCol, GridLayoutModel model)
        {
            int[,] indices = model.CellChildMap;

            int endRow = foundRow + 1;
            while (endRow < model.Rows && indices[endRow, foundCol] == indices[endRow - 1, foundCol])
            {
                endRow++;
            }

            int endCol = foundCol + 1;
            while (endCol < model.Columns && indices[foundRow, endCol] == indices[foundRow, endCol - 1])
            {
                endCol++;
            }

            int index = (orientation == Orientation.Horizontal) ? foundRow : foundCol + model.Rows - 1;
            AddDragHandle(orientation, foundRow, endRow, foundCol, endCol, index);
        }

        public void AddDragHandle(Orientation orientation, int rowStart, int rowEnd, int colStart, int colEnd, int index)
        {
            GridResizer resizer = new GridResizer
            {
                Orientation = orientation,
                StartRow = rowStart,
                EndRow = rowEnd,
                StartCol = colStart,
                EndCol = colEnd,
            };

            resizer.DragDelta += (obj, eventArgs) => _dragDelta(obj, eventArgs);
            resizer.DragCompleted += (obj, eventArgs) => _dragCompleted(obj, eventArgs);

            if (index > _resizers.Count)
            {
                index = _resizers.Count;
            }

            _resizers.Insert(index, resizer);
        }

        public void UpdateForExistingVerticalSplit(GridLayoutModel model, int foundRow, int splitCol)
        {
            Func<GridResizer, bool> cmpr = (GridResizer resizer) =>
            {
                return resizer.Orientation == Orientation.Vertical && resizer.StartCol == splitCol;
            };

            Func<GridResizer, bool> endCmpr = (GridResizer resizer) =>
            {
                return resizer.EndRow == foundRow;
            };

            Func<GridResizer, bool> startCmpr = (GridResizer resizer) =>
            {
                return resizer.StartRow == foundRow + 1;
            };

            if (!UpdateDragHandlerForExistingSplit(Orientation.Vertical, cmpr, endCmpr, startCmpr))
            {
                AddDragHandle(Orientation.Vertical, foundRow, splitCol, model);
            }
        }

        public void UpdateForExistingHorizontalSplit(GridLayoutModel model, int splitRow, int foundCol)
        {
            Func<GridResizer, bool> cmpr = (GridResizer resizer) =>
            {
                return resizer.Orientation == Orientation.Horizontal && resizer.StartRow == splitRow;
            };

            Func<GridResizer, bool> endCmpr = (GridResizer resizer) =>
            {
                return resizer.EndCol == foundCol;
            };

            Func<GridResizer, bool> startCmpr = (GridResizer resizer) =>
            {
                return resizer.StartCol == foundCol + 1;
            };

            if (!UpdateDragHandlerForExistingSplit(Orientation.Horizontal, cmpr, endCmpr, startCmpr))
            {
                AddDragHandle(Orientation.Horizontal, splitRow, foundCol, model);
            }
        }

        /**
         * Has to be called on split before adding new drag handle
         */
        public void UpdateAfterVerticalSplit(int foundCol)
        {
            foreach (GridResizer r in _resizers)
            {
                if (r.StartCol > foundCol || (r.StartCol == foundCol && r.Orientation == Orientation.Vertical))
                {
                    r.StartCol++;
                }

                if (r.EndCol > foundCol)
                {
                    r.EndCol++;
                }
            }
        }

        /**
         * Has to be called on split before adding new drag handle
         */
        public void UpdateAfterHorizontalSplit(int foundRow)
        {
            foreach (GridResizer r in _resizers)
            {
                if (r.StartRow > foundRow || (r.StartRow == foundRow && r.Orientation == Orientation.Horizontal))
                {
                    r.StartRow++;
                }

                if (r.EndRow > foundRow)
                {
                    r.EndRow++;
                }
            }
        }

        public void UpdateAfterSwap(GridResizer resizer, double delta)
        {
            Orientation orientation = resizer.Orientation;
            bool isHorizontal = orientation == Orientation.Horizontal;
            bool isDeltaNegative = delta < 0;
            List<GridResizer> swappedResizers = new List<GridResizer>();

            if (isDeltaNegative)
            {
                DecreaseResizerValues(resizer, orientation);
            }
            else
            {
                IncreaseResizerValues(resizer, orientation);
            }

            // same orientation resizers update
            foreach (GridResizer r in _resizers)
            {
                if (r.Orientation == orientation)
                {
                    if ((isHorizontal && r.StartRow == resizer.StartRow && r.StartCol != resizer.StartCol) ||
                        (!isHorizontal && r.StartCol == resizer.StartCol && r.StartRow != resizer.StartRow))
                    {
                        if (isDeltaNegative)
                        {
                            IncreaseResizerValues(r, orientation);
                        }
                        else
                        {
                            DecreaseResizerValues(r, orientation);
                        }

                        swappedResizers.Add(r);
                    }
                }
            }

            // different orientation resizers update
            foreach (GridResizer r in _resizers)
            {
                if (r.Orientation != resizer.Orientation)
                {
                    if (isHorizontal)
                    {
                        // vertical resizers corresponding to dragged resizer
                        if (r.StartCol >= resizer.StartCol && r.EndCol < resizer.EndCol)
                        {
                            if (r.StartRow == resizer.StartRow + 2 && isDeltaNegative)
                            {
                                r.StartRow--;
                            }

                            if (r.EndRow == resizer.EndRow + 1 && isDeltaNegative)
                            {
                                r.EndRow--;
                            }

                            if (r.StartRow == resizer.StartRow && !isDeltaNegative)
                            {
                                r.StartRow++;
                            }

                            if (r.EndRow == resizer.EndRow - 1 && !isDeltaNegative)
                            {
                                r.EndRow++;
                            }
                        }
                        else
                        {
                            // vertical resizers corresponding to swapped resizers
                            foreach (GridResizer sr in swappedResizers)
                            {
                                if (r.StartCol >= sr.StartCol && r.EndCol <= sr.EndCol)
                                {
                                    if (r.StartRow == resizer.StartRow + 1 && isDeltaNegative)
                                    {
                                        r.StartRow++;
                                    }

                                    if (r.EndRow == resizer.EndRow && isDeltaNegative)
                                    {
                                        r.EndRow++;
                                    }

                                    if (r.StartRow == resizer.StartRow + 1 && !isDeltaNegative)
                                    {
                                        r.StartRow--;
                                    }

                                    if (r.EndRow == resizer.EndRow && !isDeltaNegative)
                                    {
                                        r.EndRow--;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // horizontal resizers corresponding to dragged resizer
                        if (r.StartRow >= resizer.StartRow && r.EndRow < resizer.EndRow)
                        {
                            if (r.StartCol == resizer.StartCol + 3 && isDeltaNegative)
                            {
                                r.StartCol--;
                            }

                            if (r.EndCol == resizer.EndCol + 1 && isDeltaNegative)
                            {
                                r.EndCol--;
                            }

                            if (r.StartCol == resizer.StartCol && !isDeltaNegative)
                            {
                                r.StartCol++;
                            }

                            if (r.EndCol == resizer.EndCol - 1 && !isDeltaNegative)
                            {
                                r.EndCol++;
                            }
                        }
                        else
                        {
                            // horizontal resizers corresponding to swapped resizers
                            foreach (GridResizer sr in swappedResizers)
                            {
                                if (r.StartRow >= sr.StartRow && r.EndRow <= sr.EndRow)
                                {
                                    if (r.StartCol == resizer.StartCol + 1 && isDeltaNegative)
                                    {
                                        r.StartCol++;
                                    }

                                    if (r.EndCol == resizer.EndCol && isDeltaNegative)
                                    {
                                        r.EndCol++;
                                    }

                                    if (r.StartCol == resizer.StartCol + 1 && !isDeltaNegative)
                                    {
                                        r.StartCol--;
                                    }

                                    if (r.EndCol == resizer.EndCol && !isDeltaNegative)
                                    {
                                        r.EndCol--;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void UpdateAfterDetach(GridResizer resizer, double delta)
        {
            bool isDeltaNegative = delta < 0;
            Orientation orientation = resizer.Orientation;

            foreach (GridResizer r in _resizers)
            {
                bool notEqual = r.StartRow != resizer.StartRow || r.EndRow != resizer.EndRow || r.StartCol != resizer.StartCol || r.EndCol != resizer.EndCol;
                if (r.Orientation == orientation && notEqual)
                {
                    if (orientation == Orientation.Horizontal)
                    {
                        if (r.StartRow > resizer.StartRow || (r.StartRow == resizer.StartRow && isDeltaNegative))
                        {
                            r.StartRow++;
                        }

                        if (r.EndRow > resizer.EndRow || (r.EndRow == resizer.EndRow && isDeltaNegative))
                        {
                            r.EndRow++;
                        }
                    }
                    else
                    {
                        if (r.StartCol > resizer.StartCol || (r.StartCol == resizer.StartCol && isDeltaNegative))
                        {
                            r.StartCol++;
                        }

                        if (r.EndCol > resizer.EndCol || (r.EndCol == resizer.EndCol && isDeltaNegative))
                        {
                            r.EndCol++;
                        }
                    }
                }
            }

            if (!isDeltaNegative)
            {
                IncreaseResizerValues(resizer, orientation);
            }

            foreach (GridResizer r in _resizers)
            {
                if (r.Orientation != orientation)
                {
                    if (orientation == Orientation.Vertical)
                    {
                        if (isDeltaNegative)
                        {
                            bool isRowNonAdjacent = r.EndRow < resizer.StartRow || r.StartRow > resizer.EndRow;

                            if (r.StartCol > resizer.StartCol + 1 || (r.StartCol == resizer.StartCol + 1 && isRowNonAdjacent))
                            {
                                r.StartCol++;
                            }

                            if (r.EndCol > resizer.EndCol || (r.EndCol == resizer.EndCol && isRowNonAdjacent))
                            {
                                r.EndCol++;
                            }
                        }
                        else
                        {
                            if (r.StartCol > resizer.StartCol || (r.StartCol == resizer.StartCol && r.StartRow >= resizer.StartRow && r.EndRow <= resizer.EndRow))
                            {
                                r.StartCol++;
                            }

                            if (r.EndCol > resizer.EndCol - 1 || (r.EndCol == resizer.EndCol - 1 && r.StartRow >= resizer.StartRow && r.EndRow <= resizer.EndRow))
                            {
                                r.EndCol++;
                            }
                        }
                    }
                    else
                    {
                        if (isDeltaNegative)
                        {
                            bool isColNonAdjacent = r.EndCol < resizer.StartCol || r.StartCol > resizer.EndCol;

                            if (r.StartRow > resizer.StartRow + 1 || (r.StartRow == resizer.StartRow + 1 && isColNonAdjacent))
                            {
                                r.StartRow++;
                            }

                            if (r.EndRow > resizer.EndRow || (r.EndRow == resizer.EndRow && isColNonAdjacent))
                            {
                                r.EndRow++;
                            }
                        }
                        else
                        {
                            if (r.StartRow > resizer.StartRow || (r.StartRow == resizer.StartRow && r.StartCol >= resizer.StartCol && r.EndCol <= resizer.EndCol))
                            {
                                r.StartRow++;
                            }

                            if (r.EndRow > resizer.EndRow - 1 || (r.EndRow == resizer.EndRow - 1 && r.StartCol >= resizer.StartCol && r.EndCol <= resizer.EndCol))
                            {
                                r.EndRow++;
                            }
                        }
                    }
                }
            }
        }

        public void RemoveDragHandles()
        {
            _resizers.Clear();
        }

        public bool HasSnappedNonAdjascentResizers(GridResizer resizer)
        {
            /**
             * Resizers between zones 0,1 and 4,5 are snapped to each other and not adjascent.
             * ------------------------------
             * |      0      |      1       |
             * ------------------------------
             * |          2         |   3   |
             * ------------------------------
             * |      4      |      5       |
             * ------------------------------
             *
             * Resizers between zones 0,1 and 2,3 are snapped to each other and adjascent.
             * ------------------------------
             * |      0      |      1       |
             * ------------------------------
             * |      2      |      3       |
             * ------------------------------
             * |          4         |   5   |
             * ------------------------------
             *
             * Vertical resizers should have same StartColumn and different StartRow.
             * Horizontal resizers should have same StartRow and different StartColumn.
             * Difference between rows or colums should be more than 1.
             */
            foreach (GridResizer r in _resizers)
            {
                if (r.Orientation == resizer.Orientation)
                {
                    bool isHorizontalSnapped = resizer.Orientation == Orientation.Horizontal && r.StartRow == resizer.StartRow && (Math.Abs(resizer.StartCol - r.StartCol) > 1);
                    bool isVerticalSnapped = resizer.Orientation == Orientation.Vertical && r.StartCol == resizer.StartCol && (Math.Abs(resizer.StartRow - r.StartRow) > 1);
                    if (isHorizontalSnapped || isVerticalSnapped)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void IncreaseResizerValues(GridResizer resizer, Orientation orientation)
        {
            if (orientation == Orientation.Vertical)
            {
                resizer.StartCol++;
                resizer.EndCol++;
            }
            else
            {
                resizer.StartRow++;
                resizer.EndRow++;
            }
        }

        private static void DecreaseResizerValues(GridResizer resizer, Orientation orientation)
        {
            if (orientation == Orientation.Vertical)
            {
                resizer.StartCol--;
                resizer.EndCol--;
            }
            else
            {
                resizer.StartRow--;
                resizer.EndRow--;
            }
        }

        private bool UpdateDragHandlerForExistingSplit(Orientation orientation, Func<GridResizer, bool> cmpr, Func<GridResizer, bool> endCmpr, Func<GridResizer, bool> startCmpr)
        {
            bool updCurrentResizers = false;
            GridResizer leftNeighbour = null;
            GridResizer rightNeighbour = null;

            for (int i = 0; i < _resizers.Count && (leftNeighbour == null || rightNeighbour == null); i++)
            {
                GridResizer resizer = (GridResizer)_resizers[i];
                if (cmpr(resizer))
                {
                    if (leftNeighbour == null && endCmpr(resizer))
                    {
                        leftNeighbour = resizer;
                        updCurrentResizers = true;
                    }

                    if (rightNeighbour == null && startCmpr(resizer))
                    {
                        rightNeighbour = resizer;
                        updCurrentResizers = true;
                    }
                }
            }

            if (updCurrentResizers)
            {
                if (leftNeighbour != null && rightNeighbour != null)
                {
                    if (orientation == Orientation.Vertical)
                    {
                        leftNeighbour.EndRow = rightNeighbour.EndRow;
                    }
                    else
                    {
                        leftNeighbour.EndCol = rightNeighbour.EndCol;
                    }

                    _resizers.Remove(rightNeighbour);
                }
                else if (leftNeighbour != null)
                {
                    if (orientation == Orientation.Vertical)
                    {
                        leftNeighbour.EndRow++;
                    }
                    else
                    {
                        leftNeighbour.EndCol++;
                    }
                }
                else if (rightNeighbour != null)
                {
                    if (orientation == Orientation.Vertical)
                    {
                        rightNeighbour.StartRow--;
                    }
                    else
                    {
                        rightNeighbour.StartCol--;
                    }
                }
            }

            return updCurrentResizers;
        }

        private readonly UIElementCollection _resizers;
        private readonly Action<object, DragDeltaEventArgs> _dragDelta;
        private readonly Action<object, DragCompletedEventArgs> _dragCompleted;
    }
}
