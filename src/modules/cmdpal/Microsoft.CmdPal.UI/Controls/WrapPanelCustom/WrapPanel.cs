// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Controls;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

using ToolkitStretchChild = CommunityToolkit.WinUI.Controls.StretchChild;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// Arranges elements by wrapping them to fit the available space.
/// When <see cref="Orientation"/> is set to Orientation.Horizontal, element are arranged in rows until the available width is reached and then to a new row.
/// When <see cref="Orientation"/> is set to Orientation.Vertical, element are arranged in columns until the available height is reached.
/// </summary>
public sealed partial class WrapPanel : Panel
{
    private struct UvRect
    {
        public UvMeasure Position { get; set; }

        public UvMeasure Size { get; set; }

        public Rect ToRect(Orientation orientation)
        {
            return orientation switch
            {
                Orientation.Vertical => new Rect(Position.V, Position.U, Size.V, Size.U),
                Orientation.Horizontal => new Rect(Position.U, Position.V, Size.U, Size.V),
                _ => ThrowArgumentException(),
            };
        }

        private static Rect ThrowArgumentException()
        {
            throw new ArgumentException("The input orientation is not valid.");
        }
    }

    private struct Row
    {
        public List<UvRect> ChildrenRects { get; }

        public UvMeasure Size { get; set; }

        public UvRect Rect
        {
            get
            {
                UvRect result;
                if (ChildrenRects.Count <= 0)
                {
                    result = default(UvRect);
                    result.Position = UvMeasure.Zero;
                    result.Size = Size;
                    return result;
                }

                result = default(UvRect);
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

    /// <summary>
    /// Gets or sets a uniform Horizontal distance (in pixels) between items when <see cref="Orientation"/> is set to Horizontal,
    /// or between columns of items when <see cref="Orientation"/> is set to Vertical.
    /// </summary>
    public double HorizontalSpacing
    {
        get { return (double)GetValue(HorizontalSpacingProperty); }
        set { SetValue(HorizontalSpacingProperty, value); }
    }

    private bool IsSectionItem(UIElement element) => element is FrameworkElement fe && fe.DataContext is ListItemViewModel item && item.IsSectionOrSeparator;

    /// <summary>
    /// Identifies the <see cref="HorizontalSpacing"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty HorizontalSpacingProperty =
        DependencyProperty.Register(
            nameof(HorizontalSpacing),
            typeof(double),
            typeof(WrapPanel),
            new PropertyMetadata(0d, LayoutPropertyChanged));

    /// <summary>
    /// Gets or sets a uniform Vertical distance (in pixels) between items when <see cref="Orientation"/> is set to Vertical,
    /// or between rows of items when <see cref="Orientation"/> is set to Horizontal.
    /// </summary>
    public double VerticalSpacing
    {
        get { return (double)GetValue(VerticalSpacingProperty); }
        set { SetValue(VerticalSpacingProperty, value); }
    }

    /// <summary>
    /// Identifies the <see cref="VerticalSpacing"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty VerticalSpacingProperty =
        DependencyProperty.Register(
            nameof(VerticalSpacing),
            typeof(double),
            typeof(WrapPanel),
            new PropertyMetadata(0d, LayoutPropertyChanged));

    /// <summary>
    /// Gets or sets the orientation of the WrapPanel.
    /// Horizontal means that child controls will be added horizontally until the width of the panel is reached, then a new row is added to add new child controls.
    /// Vertical means that children will be added vertically until the height of the panel is reached, then a new column is added.
    /// </summary>
    public Orientation Orientation
    {
        get { return (Orientation)GetValue(OrientationProperty); }
        set { SetValue(OrientationProperty, value); }
    }

    /// <summary>
    /// Identifies the <see cref="Orientation"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(WrapPanel),
            new PropertyMetadata(Orientation.Horizontal, LayoutPropertyChanged));

    /// <summary>
    /// Gets or sets the distance between the border and its child object.
    /// </summary>
    /// <returns>
    /// The dimensions of the space between the border and its child as a Thickness value.
    /// Thickness is a structure that stores dimension values using pixel measures.
    /// </returns>
    public Thickness Padding
    {
        get { return (Thickness)GetValue(PaddingProperty); }
        set { SetValue(PaddingProperty, value); }
    }

    /// <summary>
    /// Identifies the Padding dependency property.
    /// </summary>
    /// <returns>The identifier for the <see cref="Padding"/> dependency property.</returns>
    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(WrapPanel),
            new PropertyMetadata(default(Thickness), LayoutPropertyChanged));

    /// <summary>
    /// Gets or sets a value indicating how to arrange child items
    /// </summary>
    public ToolkitStretchChild StretchChild
    {
        get { return (ToolkitStretchChild)GetValue(StretchChildProperty); }
        set { SetValue(StretchChildProperty, value); }
    }

    /// <summary>
    /// Identifies the <see cref="StretchChild"/> dependency property.
    /// </summary>
    /// <returns>The identifier for the <see cref="StretchChild"/> dependency property.</returns>
    public static readonly DependencyProperty StretchChildProperty =
        DependencyProperty.Register(
            nameof(StretchChild),
            typeof(ToolkitStretchChild),
            typeof(WrapPanel),
            new PropertyMetadata(ToolkitStretchChild.None, LayoutPropertyChanged));

    /// <summary>
    /// Identifies the IsFullLine attached dependency property.
    /// If true, the child element will occupy the entire width of the panel and force a line break before and after itself.
    /// </summary>
    public static readonly DependencyProperty IsFullLineProperty =
        DependencyProperty.RegisterAttached(
            "IsFullLine",
            typeof(bool),
            typeof(WrapPanel),
            new PropertyMetadata(false, OnIsFullLineChanged));

    public static bool GetIsFullLine(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsFullLineProperty);
    }

    public static void SetIsFullLine(DependencyObject obj, bool value)
    {
        obj.SetValue(IsFullLineProperty, value);
    }

    private static void OnIsFullLineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (FindVisualParentWrapPanel(d) is WrapPanel wp)
        {
            wp.InvalidateMeasure();
        }
    }

    private static WrapPanel? FindVisualParentWrapPanel(DependencyObject child)
    {
        var parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(child);

        while (parent != null)
        {
            if (parent is WrapPanel wrapPanel)
            {
                return wrapPanel;
            }

            parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    private static void LayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WrapPanel wp)
        {
            wp.InvalidateMeasure();
            wp.InvalidateArrange();
        }
    }

    private readonly List<Row> _rows = new List<Row>();

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var childAvailableSize = new Size(
            availableSize.Width - Padding.Left - Padding.Right,
            availableSize.Height - Padding.Top - Padding.Bottom);
        foreach (var child in Children)
        {
            child.Measure(childAvailableSize);
        }

        var requiredSize = UpdateRows(availableSize);
        return requiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if ((Orientation == Orientation.Horizontal && finalSize.Width < DesiredSize.Width) ||
            (Orientation == Orientation.Vertical && finalSize.Height < DesiredSize.Height))
        {
            // We haven't received our desired size. We need to refresh the rows.
            UpdateRows(finalSize);
        }

        if (_rows.Count > 0)
        {
            // Now that we have all the data, we do the actual arrange pass
            var childIndex = 0;
            foreach (var row in _rows)
            {
                foreach (var rect in row.ChildrenRects)
                {
                    var child = Children[childIndex++];
                    while (child.Visibility == Visibility.Collapsed)
                    {
                        // Collapsed children are not added into the rows,
                        // we skip them.
                        child = Children[childIndex++];
                    }

                    var arrangeRect = new UvRect
                    {
                        Position = rect.Position,
                        Size = new UvMeasure { U = rect.Size.U, V = row.Size.V },
                    };

                    var finalRect = arrangeRect.ToRect(Orientation);
                    child.Arrange(finalRect);
                }
            }
        }

        return finalSize;
    }

    private Size UpdateRows(Size availableSize)
    {
        _rows.Clear();

        var paddingStart = new UvMeasure(Orientation, Padding.Left, Padding.Top);
        var paddingEnd = new UvMeasure(Orientation, Padding.Right, Padding.Bottom);

        if (Children.Count == 0)
        {
            return paddingStart.Add(paddingEnd).ToSize(Orientation);
        }

        var parentMeasure = new UvMeasure(Orientation, availableSize.Width, availableSize.Height);
        var spacingMeasure = new UvMeasure(Orientation, HorizontalSpacing, VerticalSpacing);
        var position = new UvMeasure(Orientation, Padding.Left, Padding.Top);

        var currentRow = new Row(new List<UvRect>(), default);
        var finalMeasure = new UvMeasure(Orientation, width: 0.0, height: 0.0);

        void CommitRow()
        {
            // Only adds if the row has a content
            if (currentRow.ChildrenRects.Count > 0)
            {
                _rows.Add(currentRow);

                position.V += currentRow.Size.V + spacingMeasure.V;
            }

            position.U = paddingStart.U;

            currentRow = new Row(new List<UvRect>(), default);
        }

        void Arrange(UIElement child, bool isLast = false)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                return;
            }

            var isFullLine = IsSectionItem(child);
            var desiredMeasure = new UvMeasure(Orientation, child.DesiredSize);

            if (isFullLine)
            {
                if (currentRow.ChildrenRects.Count > 0)
                {
                    CommitRow();
                }

                // Forces the width to fill all the available space
                // (Total width - Padding Left - Padding Right)
                desiredMeasure.U = parentMeasure.U - paddingStart.U - paddingEnd.U;

                // Adds the Section Header to the row
                currentRow.Add(position, desiredMeasure);

                // Updates the global measures
                position.U += desiredMeasure.U + spacingMeasure.U;
                finalMeasure.U = Math.Max(finalMeasure.U, position.U);

                CommitRow();
            }
            else
            {
                // Checks if the item can fit in the row
                if ((desiredMeasure.U + position.U + paddingEnd.U) > parentMeasure.U)
                {
                    CommitRow();
                }

                if (isLast)
                {
                    desiredMeasure.U = parentMeasure.U - position.U;
                }

                currentRow.Add(position, desiredMeasure);

                position.U += desiredMeasure.U + spacingMeasure.U;
                finalMeasure.U = Math.Max(finalMeasure.U, position.U);
            }
        }

        var lastIndex = Children.Count - 1;
        for (var i = 0; i < lastIndex; i++)
        {
            Arrange(Children[i]);
        }

        Arrange(Children[lastIndex], StretchChild == ToolkitStretchChild.Last);

        if (currentRow.ChildrenRects.Count > 0)
        {
            _rows.Add(currentRow);
        }

        if (_rows.Count == 0)
        {
            return paddingStart.Add(paddingEnd).ToSize(Orientation);
        }

        var lastRowRect = _rows.Last().Rect;
        finalMeasure.V = lastRowRect.Position.V + lastRowRect.Size.V;
        return finalMeasure.Add(paddingEnd).ToSize(Orientation);
    }
}
