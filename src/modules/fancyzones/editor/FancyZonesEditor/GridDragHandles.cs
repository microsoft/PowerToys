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
                            AddDragHandle(Orientation.Horizontal, row, row + 1, col, col + 1, row);
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
                            AddDragHandle(Orientation.Vertical, row, row + 1, col, col + 1, col + model.Rows - 1);
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

            if (!UpdateDragHanlderForExistingSplit(Orientation.Vertical, cmpr, endCmpr, startCmpr))
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

            if (!UpdateDragHanlderForExistingSplit(Orientation.Horizontal, cmpr, endCmpr, startCmpr))
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

        public void UpdateAfterNegativeSwap(GridResizer resizer, double delta)
        {
            Action<GridResizer> incValues = r =>
            {
                if (resizer.Orientation == Orientation.Vertical)
                {
                    r.StartCol++;
                    r.EndCol++;
                }
                else
                {
                    r.StartRow++;
                    r.EndRow++;
                }
            };

            Action<GridResizer> decValues = r =>
            {
                if (resizer.Orientation == Orientation.Vertical)
                {
                    r.StartCol--;
                    r.EndCol--;
                }
                else
                {
                    r.StartRow--;
                    r.EndRow--;
                }
            };

            Action<GridResizer> horizontalResizersRowsUpd = r =>
            {
                if (r.StartCol == resizer.StartCol)
                {
                    r.StartCol++;
                }
                else if (r.StartCol == resizer.EndCol)
                {
                    r.StartCol--;
                }

                if (r.EndCol == resizer.StartCol)
                {
                    r.EndCol++;
                }
                else if (r.EndCol == resizer.EndCol)
                {
                    r.EndCol--;
                }
            };

            Action<GridResizer> verticalResizersColumnsUpd = r =>
            {
                if (r.StartRow == resizer.StartRow)
                {
                    r.StartRow++;
                }
                else if (r.StartRow == resizer.EndRow)
                {
                    r.StartRow--;
                }

                if (r.EndRow == resizer.StartRow)
                {
                    r.EndRow++;
                }
                else if (r.EndRow == resizer.EndRow)
                {
                    r.EndRow--;
                }
            };

            Action differentOrientationResizersUpdate = () =>
            {
                foreach (GridResizer r in _resizers)
                {
                    if (r.Orientation != resizer.Orientation)
                    {
                        if (resizer.Orientation == Orientation.Vertical)
                        {
                            horizontalResizersRowsUpd(r);
                        }
                        else
                        {
                            verticalResizersColumnsUpd(r);
                        }
                    }
                }
            };

            Action<bool> sameOrientationResizersUpdate = isDeltaNegative =>
            {
                foreach (GridResizer r in _resizers)
                {
                    bool isSameCol = resizer.StartRow != r.StartRow && resizer.StartCol == r.StartCol && resizer.Orientation == Orientation.Vertical;
                    bool isSameRow = resizer.StartRow == r.StartRow && resizer.StartCol != r.StartCol && resizer.Orientation == Orientation.Horizontal;
                    if (r.Orientation == resizer.Orientation && (isSameCol || isSameRow))
                    {
                        if (isDeltaNegative)
                        {
                            incValues(r);
                        }
                        else
                        {
                            decValues(r);
                        }

                        break;
                    }
                }
            };

            if (delta < 0)
            {
                differentOrientationResizersUpdate();
                decValues(resizer);
                sameOrientationResizersUpdate(delta < 0);
            }
            else
            {
                incValues(resizer);
                differentOrientationResizersUpdate();
                sameOrientationResizersUpdate(delta < 0);
            }
        }

        public void UpdateAfterMerge(int startRow, int endRow, int startCol, int endCol, int rows)
        {
            int shift = 0;
            List<int> horizontalRemoved = new List<int>(), verticalRemoved = new List<int>();

            for (int i = 0; i < _resizers.Count; i++)
            {
                GridResizer resizer = (GridResizer)_resizers[i];

                bool isStartRowColRemovable = resizer.StartRow >= startRow && resizer.StartCol >= startCol;
                bool horizontalResizerToBeRemoved = resizer.Orientation == Orientation.Horizontal
                    && isStartRowColRemovable && resizer.EndRow <= endRow && resizer.EndCol - 1 <= endCol;
                bool verticalResizerToBeRemoved = resizer.Orientation == Orientation.Vertical
                    && isStartRowColRemovable && resizer.EndRow - 1 <= endRow && resizer.EndCol <= endCol;

                if (horizontalResizerToBeRemoved || verticalResizerToBeRemoved)
                {
                    if (resizer.Orientation == Orientation.Horizontal)
                    {
                        horizontalRemoved.Add(i + shift);
                        rows--;
                    }
                    else
                    {
                        verticalRemoved.Add(i - rows + 1);
                    }

                    _resizers.Remove(resizer);
                    i--;
                    shift++;
                }
            }

            foreach (GridResizer resizer in _resizers)
            {
                if (resizer.Orientation == Orientation.Horizontal)
                {
                    int diff = 0;
                    for (int i = 0; i < horizontalRemoved.Count && horizontalRemoved[i] < resizer.StartRow; i++)
                    {
                        diff++;
                    }

                    resizer.StartRow -= diff;
                    resizer.EndRow -= diff;

                    for (int i = 0; i < verticalRemoved.Count && verticalRemoved[i] < resizer.EndCol; i++)
                    {
                        if (resizer.StartCol > verticalRemoved[i])
                        {
                            resizer.StartCol--;
                        }

                        resizer.EndCol--;
                    }
                }
                else
                {
                    int diff = 0;
                    for (int i = 0; i < verticalRemoved.Count && verticalRemoved[i] < resizer.StartCol; i++)
                    {
                        diff++;
                    }

                    resizer.StartCol -= diff;
                    resizer.EndCol -= diff;

                    for (int i = 0; i < horizontalRemoved.Count && horizontalRemoved[i] < resizer.EndRow; i++)
                    {
                        if (resizer.StartRow > horizontalRemoved[i])
                        {
                            resizer.StartRow--;
                        }

                        resizer.EndRow--;
                    }
                }
            }
        }

        public void RemoveDragHandles()
        {
            _resizers.Clear();
        }

        private bool UpdateDragHanlderForExistingSplit(Orientation orientation, Func<GridResizer, bool> cmpr, Func<GridResizer, bool> endCmpr, Func<GridResizer, bool> startCmpr)
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
