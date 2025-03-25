// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <history>
//     2020-... created by Filip Jeremic (fjeremic) as "HexView.Wpf".
//     2024-... republished by @hotkidfamily as "HexBox.WinUI".
//     2025 Included in PowerToys. (Branch master; commit 72dcf64dc858c693a7a16887004c8ddbab61fce7.)
// </history>
#pragma warning disable SA1210 // Using directives should be ordered alphabetically by namespace
#pragma warning disable SA1208 // System using directives should be placed before other using directives
using RegistryPreviewUILib.HexBox.Library.EndianConvert;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
#pragma warning restore SA1208 // System using directives should be placed before other using directives
#pragma warning restore SA1210 // Using directives should be ordered alphabetically by namespace

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace RegistryPreviewUILib.HexBox
{
    [TemplatePart(Name = "ElementCanvas", Type = typeof(SKXamlCanvas))]
    [TemplatePart(Name = "ElementScrollBar", Type = typeof(ScrollBar))]
    public sealed class HexBox : Control, INotifyPropertyChanged
    {
        /// <summary>
        /// Defines the address at which the data in the <see cref="DataSourceProperty"/> begins.
        /// </summary>
        public static readonly DependencyProperty AddressProperty =
            DependencyProperty.Register(nameof(Address), typeof(ulong), typeof(HexBox),
                new PropertyMetadata(0UL, OnAddressChanged));

        /// <summary>
        /// Defines the brush used to display the addresses in the address section of the control.
        /// </summary>
        public static readonly DependencyProperty AddressBrushProperty =
            DependencyProperty.Register(nameof(AddressBrush), typeof(SolidColorBrush), typeof(HexBox),
                new PropertyMetadata(new SolidColorBrush(Colors.CornflowerBlue), OnPropertyChangedInvalidateVisual));

        /// <summary>
        /// Defines the width of the addresses displayed in the address section of the control.
        /// </summary>
        public static readonly DependencyProperty AddressFormatProperty =
            DependencyProperty.Register(nameof(AddressFormat), typeof(AddressFormat), typeof(HexBox),
                new PropertyMetadata(AddressFormat.Address32, OnPropertyChangedInvalidateVisual));

        /// <summary>
        ///  Defines the brush used for alternating for text in alternating (odd numbered) columns in the data section of the control.
        /// </summary>
        public static readonly DependencyProperty AlternatingDataColumnTextBrushProperty =
            DependencyProperty.Register(nameof(AlternatingDataColumnTextBrush), typeof(SolidColorBrush), typeof(HexBox),
                new PropertyMetadata(new SolidColorBrush(Colors.Gray), OnPropertyChangedInvalidateVisual));

        /// <summary>
        /// Defines the number of columns to display.
        /// </summary>
        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(nameof(Columns), typeof(int), typeof(HexBox),
                new PropertyMetadata(16, OnPropertyChangedInvalidateVisual));

        /// <summary>
        /// Defines the endianness used to interpret the data.
        /// </summary>
        public static readonly DependencyProperty EndiannessProperty =
            DependencyProperty.Register(nameof(Endianness), typeof(Endianness), typeof(HexBox),
                new PropertyMetadata(Endianness.BigEndian, OnPropertyChangedInvalidateVisual));

        /// <summary>
        /// Defines the format of the data to display.
        /// </summary>
        public static readonly DependencyProperty DataFormatProperty =
            DependencyProperty.Register(nameof(DataFormat), typeof(DataFormat), typeof(HexBox),
                new PropertyMetadata(DataFormat.Hexadecimal, OnPropertyChangedInvalidateVisual));

        /// <summary>
        /// Defines the signedness of the data to display.
        /// </summary>
        public static readonly DependencyProperty DataSignednessProperty =
            DependencyProperty.Register(nameof(DataSignedness), typeof(DataSignedness), typeof(HexBox),
                new PropertyMetadata(DataSignedness.Unsigned, OnPropertyChangedInvalidateVisual));

        /// <summary>
        /// Defines the data source which is used to read the data to display within this control.
        /// </summary>
        public static readonly DependencyProperty DataSourceProperty =
            DependencyProperty.Register(nameof(DataSource), typeof(BinaryReader), typeof(HexBox),
                new PropertyMetadata(null, OnDataSourceChanged));

        /// <summary>
        /// Defines the offset from the <see cref="DataSourceProperty"/> of the first visible data element being displayed.
        /// </summary>
        public static readonly DependencyProperty OffsetProperty =
            DependencyProperty.Register(nameof(Offset), typeof(long), typeof(HexBox),
                new PropertyMetadata(0L, OnPropertyChangedInvalidateVisual));

        /// <summary>
        /// Defines the maximum number of columns, based on the size of the control, which can be displayed.
        /// </summary>
        public static readonly DependencyProperty MaxVisibleColumnsProperty =
            DependencyProperty.Register(nameof(MaxVisibleColumns), typeof(int), typeof(HexBox),
                new PropertyMetadata(int.MaxValue, OnPropertyChangedInvalidateVisual));


        /// <summary>
        /// Defines the maximum number of rows, based on the size of the control, which can be displayed.
        /// </summary>
        public static readonly DependencyProperty MaxVisibleRowsProperty =
            DependencyProperty.Register(nameof(MaxVisibleRows), typeof(int), typeof(HexBox),
                new PropertyMetadata(int.MaxValue, OnPropertyChangedInvalidateVisual));

        /// <summary>
        /// Defines the brush used for selection fill.
        /// </summary>
        public static readonly DependencyProperty SelectionBrushProperty =
            DependencyProperty.Register(nameof(SelectionBrush), typeof(SolidColorBrush), typeof(HexBox),
                new PropertyMetadata(new SolidColorBrush(Colors.LightPink), OnPropertyChangedInvalidateVisual));

        /// <summary>
        /// Defines the brush used for selected text.
        /// </summary>
        public static readonly DependencyProperty SelectionTextBrushProperty =
            DependencyProperty.Register(nameof(SelectionTextBrush), typeof(SolidColorBrush), typeof(HexBox),
                new PropertyMetadata(new SolidColorBrush(Colors.Black), OnPropertyChangedInvalidateVisual));

        /// <summary>
        /// Defines the offset from <see cref="DataSourceProperty"/> of where the user selection has ended.
        /// </summary>
        public static readonly DependencyProperty SelectionEndProperty =
            DependencyProperty.Register(nameof(SelectionEnd), typeof(long), typeof(HexBox),
                new PropertyMetadata(0L, OnSelectionEndChanged));

        /// <summary>
        /// Defines the offset from <see cref="DataSourceProperty"/> of where the user selection has started.
        /// </summary>
        public static readonly DependencyProperty SelectionStartProperty =
            DependencyProperty.Register(nameof(SelectionStart), typeof(long), typeof(HexBox),
                new PropertyMetadata(0L, OnSelectionStartChanged));

        /// <summary>
        /// Determines whether the user can change the layout and data format.
        /// </summary>
        public static readonly DependencyProperty EnforcePropertiesProperty =
            DependencyProperty.Register(nameof(EnforceProperties), typeof(bool), typeof(HexBox),
                new PropertyMetadata(false, OnPropertyChangedInvalidateVisual));

        /// <summary>
        /// Determines whether to show the address section of the control.
        /// </summary>
        public static readonly DependencyProperty ShowAddressProperty =
            DependencyProperty.Register(nameof(ShowAddress), typeof(bool), typeof(HexBox),
                new PropertyMetadata(true, OnPropertyChangedInvalidateVisual));

        /// <summary>
        /// Determines whether to show the data section of the control.
        /// </summary>
        public static readonly DependencyProperty ShowDataProperty =
            DependencyProperty.Register(nameof(ShowData), typeof(bool), typeof(HexBox),
                new PropertyMetadata(true, OnPropertyChangedInvalidateVisual));

        /// <summary>
        /// Determines whether to show the text section of the control.
        /// </summary>
        public static readonly DependencyProperty ShowTextProperty =
            DependencyProperty.Register(nameof(ShowText), typeof(bool), typeof(HexBox),
                new PropertyMetadata(true, OnPropertyChangedInvalidateVisual));

        /// <summary>
        /// Defines the brush used for the fill of the vertical separator line between the areas.
        /// </summary>
        public static readonly DependencyProperty VerticalSeparatorLineBrushProperty =
            DependencyProperty.Register(nameof(VerticalSeparatorLineBrush), typeof(SolidColorBrush), typeof(HexBox),
                new PropertyMetadata(new SolidColorBrush(Colors.Black), OnPropertyChangedInvalidateVisual));


        /// <summary>
        /// Defines the format of the text to display in the text section.
        /// </summary>
        public static readonly DependencyProperty TextFormatProperty =
            DependencyProperty.Register(nameof(TextFormat), typeof(TextFormat), typeof(HexBox),
                new PropertyMetadata(TextFormat.Ascii, OnPropertyChangedInvalidateVisual));

        /// <summary>
        /// Gets the <see cref="SelectAll"/> command.
        /// </summary>
        public ICommand SelectAllCommand
        {
            get { return (ICommand)GetValue(SelectAllCommandProperty); }
            set { SetValue(SelectAllCommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CopyCommand.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectAllCommandProperty =
            DependencyProperty.Register("SelectAllCommand", typeof(ICommand), typeof(HexBox), new PropertyMetadata(null));

        /// <summary>
        /// Gets the <see cref="Copy"/> command.
        /// </summary>
        public ICommand CopyCommand
        {
            get { return (ICommand)GetValue(CopyCommandProperty); }
            set { SetValue(CopyCommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CopyCommand.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CopyCommandProperty =
            DependencyProperty.Register("CopyCommand", typeof(ICommand), typeof(HexBox), new PropertyMetadata(null));

        /// <summary>
        /// Gets the <see cref="CopyText"/> for text command.
        /// </summary>
        public ICommand CopyTextCommand
        {
            get { return (ICommand)GetValue(CopyTextCommandProperty); }
            set { SetValue(CopyTextCommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CopyTextCommand.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CopyTextCommandProperty =
            DependencyProperty.Register("CopyTextCommand", typeof(ICommand), typeof(HexBox), new PropertyMetadata(null));


        private const int _MaxColumns = 128;
        private const int _MaxRows = 128;

        private const int _CharsBetweenSections = 2;
        private const int _CharsBetweenDataColumns = 1;
        private const int _ScrollWheelScrollRows = 3;

        private Rect _AddressRect;
        private Rect _DataRect;
        private Rect _TextRect;

        private SKPaint _TextPaint;
        private SKPaint _LinePaint;
        private SKRect _TextMeasure;
        private SKTypeface _TextTypeFace;

        private SKXamlCanvas _Canvas;
        private string _CanvasName = "ElementCanvas";

        private SelectionArea _HighlightBegin = SelectionArea.None;
        private SelectionArea _HighlightState = SelectionArea.None;

        private double _LastVerticalScrollValue = 0;

        private ScrollBar _ScrollBar;
        private string _ScrollBarName = "ElementScrollBar";

        private SelectionAdjustment _pointerMoveSelectionAdjustment = SelectionAdjustment.None;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged;

        private enum SelectionArea
        {
            None,
            Address,
            Data,
            Text,
        }

        private enum SelectionAdjustment
        {
            None,
            Up,
            Down
        }

        /// <summary>
        /// Gets or sets the address at which the data in the <see cref="DataSource"/> begins.
        /// </summary>
        public ulong Address
        {
            get => (ulong)GetValue(AddressProperty);

            set => SetValue(AddressProperty, value);
        }

        /// <summary>
        /// Gets or sets the brush used to display the addresses in the address section of the control.
        /// </summary>
        public SolidColorBrush AddressBrush
        {
            get => (SolidColorBrush)GetValue(AddressBrushProperty);

            set => SetValue(AddressBrushProperty, value);
        }

        /// <summary>
        /// Gets or sets the brush used for alternating for text in alternating (odd numbered) columns in the data section of the control.
        /// </summary>
        public SolidColorBrush AlternatingDataColumnTextBrush
        {
            get => (SolidColorBrush)GetValue(AlternatingDataColumnTextBrushProperty);

            set => SetValue(AlternatingDataColumnTextBrushProperty, value);
        }

        /// <summary>
        /// Gets or sets the number of columns to display.
        /// </summary>
        public int Columns
        {
            get => (int)GetValue(ColumnsProperty);

            set => SetValue(ColumnsProperty, CoerceColumns(this, value));
        }

        /// <summary>
        /// Gets or sets the endianness used to interpret the data.
        /// </summary>
        public Endianness Endianness
        {
            get => (Endianness)GetValue(EndiannessProperty);

            set => SetValue(EndiannessProperty, value);
        }

        /// <summary>
        /// Gets or sets the format of the data to display.
        /// </summary>
        public DataFormat DataFormat
        {
            get => (DataFormat)GetValue(DataFormatProperty);

            set => SetValue(DataFormatProperty, value);
        }

        /// <summary>
        /// Gets or sets the signedness of the data to display.
        /// </summary>
        public DataSignedness DataSignedness
        {
            get => (DataSignedness)GetValue(DataSignednessProperty);

            set => SetValue(DataSignednessProperty, value);
        }

        /// <summary>
        /// Gets or sets the data source which is used to read the data to display within this control.
        /// </summary>
        public BinaryReader DataSource
        {
            get => (BinaryReader)GetValue(DataSourceProperty);

            set => SetValue(DataSourceProperty, value);
        }

        /// <summary>
        /// Gets or sets the data type which is used to display within this control.
        /// </summary>
        public DataType DataType
        {
            get { return (DataType)GetValue(DataTypeProperty); }
            set => SetValue(DataTypeProperty, value);
        }

        // Using a DependencyProperty as the backing store for DataType.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DataTypeProperty =
            DependencyProperty.Register("DataType", typeof(DataType), typeof(HexBox), new PropertyMetadata(DataType.Int_1, OnDataTypeChanged));

        /// <summary>
        /// Gets or sets the width of the data to display.
        /// </summary>
        private int DataWidth = 1;

        /// <summary>
        /// Gets a value indicating whether the user has made any selection within the control.
        /// </summary>
        public bool IsSelectionActive => SelectionLength != 0;

        /// <summary>
        /// Gets the maximum number of columns, based on the size of the control, which can be displayed.
        /// </summary>
        public int MaxVisibleColumns
        {
            get => (int)GetValue(MaxVisibleColumnsProperty);

            private set => SetValue(MaxVisibleColumnsProperty, CoerceMaxVisibleColumns(this, value));
        }

        /// <summary>
        /// Gets the maximum number of rows, based on the size of the control, which can be displayed.
        /// </summary>
        public int MaxVisibleRows
        {
            get => (int)GetValue(MaxVisibleRowsProperty);

            private set => SetValue(MaxVisibleRowsProperty, CoerceMaxVisibleRows(this, value));
        }

        /// <summary>
        /// Gets or sets the offset from the <see cref="DataSource"/> of the first visible data element being displayed.
        /// </summary>
        public long Offset
        {
            get => (long)GetValue(OffsetProperty);

            set => SetValue(OffsetProperty, CoerceOffset(this, value));
        }

        /// <summary>
        /// Gets lowest order address currently being selected.
        /// </summary>
        public ulong SelectedAddress => Address + (ulong)SelectedOffset;

        /// <summary>
        /// Gets the offset from <see cref="DataSource"/> of the <see cref="SelectedAddress"/>.
        /// </summary>
        public long SelectedOffset => Math.Min(SelectionStart, SelectionEnd);

        /// <summary>
        /// Gets or sets the brush used for selection fill.
        /// </summary>
        public SolidColorBrush SelectionBrush
        {
            get => (SolidColorBrush)GetValue(SelectionBrushProperty);

            set => SetValue(SelectionBrushProperty, value);
        }

        /// <summary>
        /// Gets the offset from <see cref="DataSource"/> of where the user selection has ended.
        /// </summary>
        public long SelectionEnd
        {
            get => (long)GetValue(SelectionEndProperty);

            private set => SetValue(SelectionEndProperty, CoerceSelectionEnd(this, value));
        }

        /// <summary>
        /// Gets the number of bytes selected.
        /// </summary>
        public long SelectionLength
        {
            get
            {
                if (SelectionStart <= SelectionEnd)
                {
                    return SelectionEnd - SelectionStart;
                }
                else
                {
                    return SelectionStart - SelectionEnd + _BytesPerColumn;
                }
            }
        }

        /// <summary>
        /// Gets the offset from <see cref="DataSource"/> of where the user selection has started.
        /// </summary>
        public long SelectionStart
        {
            get => (long)GetValue(SelectionStartProperty);

            private set
            {
                SetValue(SelectionStartProperty, CoerceSelectionStart(this, value));

                // Reset SelectionStart adjustment state
                _pointerMoveSelectionAdjustment = SelectionAdjustment.None;
            }
        }

        /// <summary>
        /// Gets or sets the brush used for selected text.
        /// </summary>
        public SolidColorBrush SelectionTextBrush
        {
            get => (SolidColorBrush)GetValue(SelectionTextBrushProperty);

            set => SetValue(SelectionTextBrushProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the user can change the layout and data format or not.
        /// </summary>
        public bool EnforceProperties
        {
            get => (bool)GetValue(EnforcePropertiesProperty);
            set => SetValue(EnforcePropertiesProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether to show the address section of the control.
        /// </summary>
        public bool ShowAddress
        {
            get => (bool)GetValue(ShowAddressProperty);

            set => SetValue(ShowAddressProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether to show the data section of the control.
        /// </summary>
        public bool ShowData
        {
            get => (bool)GetValue(ShowDataProperty);

            set => SetValue(ShowDataProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether to show the text section of the control.
        /// </summary>
        public bool ShowText
        {
            get => (bool)GetValue(ShowTextProperty);

            set => SetValue(ShowTextProperty, value);
        }

        /// <summary>
        /// Gets or sets the brush used to display the vertical separator line between the control areas.
        /// </summary>
        public SolidColorBrush VerticalSeparatorLineBrush
        {
            get => (SolidColorBrush)GetValue(VerticalSeparatorLineBrushProperty);

            set => SetValue(VerticalSeparatorLineBrushProperty, value);
        }

        /// <summary>
        /// Gets or sets the width of the addresses displayed in the address section of the control.
        /// </summary>
        public AddressFormat AddressFormat
        {
            get => (AddressFormat)GetValue(AddressFormatProperty);

            set => SetValue(AddressFormatProperty, value);
        }

        /// <summary>
        /// Gets or sets the format of the text to display in the text section.
        /// </summary>
        public TextFormat TextFormat
        {
            get => (TextFormat)GetValue(TextFormatProperty);

            set => SetValue(TextFormatProperty, value);
        }

        private double _SelectionBoxDataXPadding => _TextMeasure.Width / 4;

        private double _SelectionBoxDataYPadding => 0;

        private double _SelectionBoxTextXPadding => 0;

        private double _SelectionBoxTextYPadding => 0;

        private int _BytesPerColumn => DataWidth;

        private int _BytesPerRow => DataWidth * Columns;

        public class HighlightedRegion
        {
            public long Start;
            public long Length;
            public long End { get { return Start + Length; } }
            public Brush Color;

            public HighlightedRegion()
            {

            }

            public HighlightedRegion(int Start, int Length, Brush Color)
            {
                this.Start = Start;
                this.Length = Length;
                this.Color = Color;
            }

            public bool IsByteSelected(long BytePos)
            {
                return BytePos >= Start && BytePos <= End;
            }
        }

        public List<HighlightedRegion> HighlightedRegions
        {
            get { return (List<HighlightedRegion>)GetValue(HighlightedRegionsProperty); }
            set { SetValue(HighlightedRegionsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for HighlightedRegions.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HighlightedRegionsProperty =
            DependencyProperty.Register("HighlightedRegions", typeof(List<HighlightedRegion>), typeof(HexBox), new PropertyMetadata(new List<HighlightedRegion>(), OnPropertyChangedInvalidateVisual));


        /// <summary>
        /// Clears the current selection
        /// </summary>
        public void ClearSelection()
        {
            SelectionStart = SelectionEnd = 0;
        }


        /// <summary>
        /// Select all data.
        /// </summary>
        public void SelectAll()
        {
            SelectionStart = 0;
            SelectionEnd = DataSource.BaseStream.Length;
        }


        /// <summary>
        /// Copies the current selection of the control to the <see cref="Clipboard"/>.
        /// </summary>
        /// <param name="copyText">Copy the text and not the data.</param>
        public void Copy(bool copyText)
        {
            if (IsSelectionActive)
            {
                StringBuilder builder = new();

                long savedDataSourcePositionBeforeReadingData = DataSource.BaseStream.Position;

                // Adjust wrong SelectionEnd after selecting down or left to right
                long selectionEnd = SelectionStart < SelectionEnd ? SelectionEnd - _BytesPerColumn : SelectionEnd;

                DataSource.BaseStream.Position = Math.Min(SelectionStart, selectionEnd);

                while (DataSource.BaseStream.Position <= Math.Max(SelectionStart, selectionEnd))
                {
                    if (copyText)
                    {
                        var formattedData = ReadFormattedText();
                        builder.Append(formattedData);
                    }
                    else
                    {
                        var formattedData = ReadFormattedData();
                        builder.Append(formattedData);
                    }
                }

                DataSource.BaseStream.Position = savedDataSourcePositionBeforeReadingData;

                var dataPackage = new DataPackage();
                dataPackage.SetText(builder.ToString());
                Clipboard.SetContent(dataPackage);
            }
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _Canvas = GetTemplateChild(_CanvasName) as SKXamlCanvas;

            if (_Canvas != null)
            {
                CopyCommand = new RelayCommand(CopyExecuted, CopyCanExecute);
                CopyTextCommand = new RelayCommand(CopyTextExecuted, CopyCanExecute);
                SelectAllCommand = new RelayCommand(SelectAllExecuted, SelectAllCanExecute);
                _Canvas.PaintSurface += Canvas_PaintSurface;
            }
            else
            {
                throw new InvalidOperationException($"Could not find {_CanvasName} template child.");
            }

            if (_ScrollBar != null)
            {
                _ScrollBar.Scroll -= OnVerticalScrollBarScroll;
            }

            _ScrollBar = GetTemplateChild(_ScrollBarName) as ScrollBar;

            if (_ScrollBar != null)
            {
                _ScrollBar.Scroll += OnVerticalScrollBarScroll;
                _ScrollBar.ValueChanged += OnVerticalScrollBarValueChanged;

                _ScrollBar.Minimum = 0;
                _ScrollBar.SmallChange = 1;
                _ScrollBar.LargeChange = MaxVisibleRows;
                _TextTypeFace = SKTypeface.FromFamilyName(_ScrollBar.FontFamily.Source, SKFontStyle.Normal);
            }
            else
            {
                throw new InvalidOperationException($"Could not find {_ScrollBarName} template child.");
            }
        }

        private void DrawSelectionGeometry(SKCanvas Canvas,
                                            Brush brush,
                                            SKPaint pen,
                                            Point point0,
                                            Point point1,
                                            SelectionArea relativeTo)
        {
            if ((long)point0.Y > (long)point1.Y)
            {
                throw new ArgumentException($"{point0.ToString()} > {point1.ToString()}", nameof(point0));
            }

            Point lhsVerticalLinePoint0;
            Point rhsVerticalLinePoint0;

            double selectionBoxXPadding;
            double selectionBoxYPadding;

            switch (relativeTo)
            {
                case SelectionArea.Data:
                    {
                        lhsVerticalLinePoint0 = new Point(_AddressRect.Left, _AddressRect.Top);
                        rhsVerticalLinePoint0 = new Point(_DataRect.Left, _DataRect.Top);

                        selectionBoxXPadding = _SelectionBoxDataXPadding;
                        selectionBoxYPadding = _SelectionBoxDataYPadding;
                    }

                    break;

                case SelectionArea.Text:
                    {
                        lhsVerticalLinePoint0 = new Point(_DataRect.Left, _DataRect.Top);
                        rhsVerticalLinePoint0 = new Point(_TextRect.Left, _TextRect.Top);

                        selectionBoxXPadding = _SelectionBoxTextXPadding;
                        selectionBoxYPadding = _SelectionBoxTextYPadding;
                    }

                    break;

                default:
                    {
                        throw new ArgumentException($"Invalid relative area {relativeTo}", nameof(relativeTo));
                    }
            }

            point0.X -= selectionBoxXPadding;
            point1.X += selectionBoxXPadding;
            point0.Y -= selectionBoxYPadding;
            point1.Y += selectionBoxYPadding;

            var ps_CharsBetweenSections = _CharsBetweenSections * _TextMeasure.Width;

            SKPath path = new();
            SKPoint[] points;

            if ((long)point0.X < (long)point1.X)
            {
                if ((long)point0.Y < (long)point1.Y)
                {
                    // +---------------------------+
                    // |                           |
                    // |             0-------------2
                    // |             |             |
                    // 6-------------7     1-------3
                    // |                   |       |
                    // 5-------------------4       |
                    // |                           |
                    // |                           |
                    // |                           |
                    // +---------------------------+
                    Point point2 = new(rhsVerticalLinePoint0.X - ps_CharsBetweenSections + selectionBoxXPadding, point0.Y);
                    Point point3 = new(rhsVerticalLinePoint0.X - ps_CharsBetweenSections + selectionBoxXPadding, point1.Y);
                    Point point4 = new(point1.X, point1.Y + _TextMeasure.Height);
                    Point point5 = new(lhsVerticalLinePoint0.X + ps_CharsBetweenSections - selectionBoxXPadding, point1.Y + _TextMeasure.Height);
                    Point point6 = new(lhsVerticalLinePoint0.X + ps_CharsBetweenSections - selectionBoxXPadding, point0.Y + _TextMeasure.Height);
                    Point point7 = new(point0.X, point0.Y + _TextMeasure.Height);

                    points = [point0.ToSKPoint(), point2.ToSKPoint(), point3.ToSKPoint(), point1.ToSKPoint(), point4.ToSKPoint(), point5.ToSKPoint(), point6.ToSKPoint(), point7.ToSKPoint()];
                }
                else
                {
                    // +---------------------------+
                    // |                           |
                    // |     0-------------1       |
                    // |     |             |       |
                    // |     3-------------2       |
                    // |                           |
                    // |                           |
                    // |                           |
                    // |                           |
                    // |                           |
                    // +---------------------------+
                    Point point2 = new(point1.X, point1.Y + _TextMeasure.Height);
                    Point point3 = new(point0.X, point0.Y + _TextMeasure.Height);

                    points = [point0.ToSKPoint(), point1.ToSKPoint(), point2.ToSKPoint(), point3.ToSKPoint()];
                }
            }
            else
            {
                if ((long)(point0.Y + _TextMeasure.Height) == (long)point1.Y)
                {
                    // +---------------------------+
                    // |                           |
                    // |             0-------------2
                    // |             |             |
                    // 7--------1    4-------------3
                    // |        |                  |
                    // 6--------5                  |
                    // |                           |
                    // |                           |
                    // |                           |
                    // +---------------------------+
                    {
                        Point point2 = new(rhsVerticalLinePoint0.X - ps_CharsBetweenSections + selectionBoxXPadding, point0.Y);
                        Point point3 = new(rhsVerticalLinePoint0.X - ps_CharsBetweenSections + selectionBoxXPadding, point1.Y);
                        Point point4 = new(point0.X, point1.Y);

                        points = [point0.ToSKPoint(), point2.ToSKPoint(), point3.ToSKPoint(), point4.ToSKPoint()];
                    }

                    path.AddPoly(points);

                    {
                        Point point5 = new(point1.X, point1.Y + _TextMeasure.Height);
                        Point point6 = new(lhsVerticalLinePoint0.X + ps_CharsBetweenSections - selectionBoxXPadding, point1.Y + _TextMeasure.Height);
                        Point point7 = new(lhsVerticalLinePoint0.X + ps_CharsBetweenSections - selectionBoxXPadding, point1.Y);
                        points = [point1.ToSKPoint(), point5.ToSKPoint(), point6.ToSKPoint(), point7.ToSKPoint()];
                    }
                }
                else
                {
                    // +---------------------------+
                    // |                           |
                    // |             0-------------2
                    // |             |             |
                    // 6-------------7             |
                    // |                           |
                    // |        1------------------3
                    // |        |                  |
                    // 5--------4                  |
                    // |                           |
                    // +---------------------------+
                    Point point2 = new(rhsVerticalLinePoint0.X - ps_CharsBetweenSections + selectionBoxXPadding, point0.Y);
                    Point point3 = new(rhsVerticalLinePoint0.X - ps_CharsBetweenSections + selectionBoxXPadding, point1.Y);
                    Point point4 = new(point1.X, point1.Y + _TextMeasure.Height);
                    Point point5 = new(lhsVerticalLinePoint0.X + ps_CharsBetweenSections - selectionBoxXPadding, point1.Y + _TextMeasure.Height);
                    Point point6 = new(lhsVerticalLinePoint0.X + ps_CharsBetweenSections - selectionBoxXPadding, point0.Y + _TextMeasure.Height);
                    Point point7 = new(point0.X, point0.Y + _TextMeasure.Height);

                    points = [point0.ToSKPoint(), point2.ToSKPoint(), point3.ToSKPoint(), point1.ToSKPoint(), point4.ToSKPoint(), point5.ToSKPoint(), point6.ToSKPoint(), point7.ToSKPoint()];
                }
            }

            path.AddPoly(points);
            if (brush is SolidColorBrush s)
                pen.Color = s.Color.ToSKColor();
            Canvas.DrawPath(path, pen);
        }

        private void DrawTextAccuracy(SKCanvas Canvas, SKPaint paint, SKPoint pt, string text)
        {
            //Canvas.DrawText(text, pt, paint);
            int index = 0;
            foreach (var c in text)
            {
                Canvas.DrawText(c.ToString(), pt.X + index * _TextMeasure.Width, pt.Y, paint);
                index++;
            }
        }

        private void Canvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var view = sender as SKXamlCanvas;
            var canvas = e.Surface.Canvas;

            if (_LinePaint == null)
            {
                _LinePaint = new()
                {
                    IsStroke = true,
                    IsAntialias = true,
                    StrokeWidth = 1,
                    TextSize = (float)FontSize,
                    Typeface = _TextTypeFace,
                    TextAlign = SKTextAlign.Left,
                };
            }
            _LinePaint.Color = VerticalSeparatorLineBrush.Color.ToSKColor();

            if (_TextPaint == null)
            {
                _TextPaint = new()
                {
                    TextSize = (float)FontSize,
                    Typeface = _TextTypeFace,
                    TextScaleX = 1f,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Left,
                    HintingLevel = SKPaintHinting.Normal,
                };
            }

            UpdateState();

            if (DataSource != null)
            {
                canvas.Clear();
                long savedDataSourcePosition = DataSource.BaseStream.Position;

                DataSource.BaseStream.Position = Offset;

                if (ShowAddress)
                {
                    var p0 = new Point(_AddressRect.Left, _AddressRect.Top).ToSKPoint();
                    var p1 = new Point(_AddressRect.Right, _AddressRect.Bottom).ToSKPoint();

                    canvas.DrawLine(p0, p1, _LinePaint);
                }

                if (ShowData)
                {
                    var p0 = new Point(_DataRect.Left, _DataRect.Top).ToSKPoint();
                    var p1 = new Point(_DataRect.Right, _DataRect.Bottom).ToSKPoint();

                    canvas.DrawLine(p0, p1, _LinePaint);

                    if (HighlightedRegions.Count != 0 && MaxVisibleRows > 0 && Columns > 0)
                    {
                        var viewLimited = Offset + _BytesPerRow * MaxVisibleRows;

                        foreach (var hlSection in HighlightedRegions)
                        {
                            if (hlSection.End <= Offset || (hlSection.Start >= viewLimited) || hlSection.Start >= hlSection.End) continue;

                            var max_visible = Math.Min(hlSection.End, viewLimited);

                            Point hlsP0 = ConvertOffsetToPosition(hlSection.Start, SelectionArea.Data);
                            Point hlsP1 = ConvertOffsetToPosition(max_visible, SelectionArea.Data);

                            if (max_visible % _BytesPerRow == 0)
                            {
                                hlsP1.X = p1.X - _CharsBetweenSections * _TextMeasure.Width;
                                hlsP1.Y = Math.Max(hlsP0.Y, hlsP1.Y - _TextMeasure.Height);
                            }
                            else
                            {
                                hlsP1.X -= _TextMeasure.Width;
                            }

                            DrawSelectionGeometry(canvas, hlSection.Color, _TextPaint, hlsP0, hlsP1, SelectionArea.Data);
                        }
                    }
                }

                if (ShowText)
                {
                    var p0 = new Point(_TextRect.Left, _TextRect.Top);
                    var p1 = new Point(_TextRect.Right, _TextRect.Bottom);

                    canvas.DrawLine(p0.ToSKPoint(), p1.ToSKPoint(), _LinePaint);

                    if (HighlightedRegions.Count != 0 && MaxVisibleRows > 0 && Columns > 0)
                    {
                        var viewLimited = Offset + MaxVisibleColumns * MaxVisibleRows;

                        foreach (var hlSection in HighlightedRegions)
                        {
                            if (hlSection.End <= Offset || (hlSection.Start >= viewLimited) || hlSection.Start >= hlSection.End) continue;

                            var max_visible = Math.Min(hlSection.End, viewLimited);

                            Point hlsP0 = ConvertOffsetToPosition(hlSection.Start, SelectionArea.Text);
                            Point hlsP1 = ConvertOffsetToPosition(max_visible, SelectionArea.Text);

                            if (max_visible % _BytesPerRow == 0)
                            {
                                hlsP1.X = p1.X - _CharsBetweenSections * _TextMeasure.Width;
                                hlsP1.Y = Math.Max(hlsP0.Y, hlsP1.Y - _TextMeasure.Height);
                            }

                            DrawSelectionGeometry(canvas, hlSection.Color, _TextPaint, hlsP0, hlsP1, SelectionArea.Text);
                        }
                    }
                }

                if (ShowData)
                {
                    if (SelectionLength != 0 && MaxVisibleRows > 0 && Columns > 0)
                    {
                        Point sp0 = ConvertOffsetToPosition(SelectedOffset, SelectionArea.Data);
                        Point sp1 = ConvertOffsetToPosition(SelectedOffset + SelectionLength, SelectionArea.Data);

                        if ((SelectedOffset + SelectionLength) % _BytesPerRow == 0)
                        {
                            sp1.X = _DataRect.Left - _CharsBetweenSections * _TextMeasure.Width;
                            sp1.Y = Math.Max(sp0.Y, sp1.Y - _TextMeasure.Height);
                        }
                        else
                        {
                            sp1.X -= _TextMeasure.Width;
                        }

                        DrawSelectionGeometry(canvas, SelectionBrush, _TextPaint, sp0, sp1, SelectionArea.Data);
                    }
                }

                if (ShowText)
                {
                    if (SelectionLength != 0 && MaxVisibleRows > 0 && Columns > 0)
                    {
                        Point sp0 = ConvertOffsetToPosition(SelectedOffset, SelectionArea.Text);
                        Point sp1 = ConvertOffsetToPosition(SelectedOffset + SelectionLength, SelectionArea.Text);

                        if ((SelectedOffset + SelectionLength) % _BytesPerRow == 0)
                        {
                            sp1.X = _TextRect.Left - _CharsBetweenSections * _TextMeasure.Width;
                            sp1.Y -= _TextMeasure.Height;
                        }

                        DrawSelectionGeometry(canvas, SelectionBrush, _TextPaint, sp0, sp1, SelectionArea.Text);
                    }
                }

                SKPoint origin = default;
                origin.Y = _TextMeasure.Height * 3 / 4; /* left bottom to right top */

                for (var row = 0; row < MaxVisibleRows; ++row)
                {
                    if (ShowAddress)
                    {
                        if (DataSource.BaseStream.Position + _BytesPerColumn <= DataSource.BaseStream.Length)
                        {
                            var textToFormat = GetFormattedAddressText(Address + (ulong)DataSource.BaseStream.Position);

                            if (AddressBrush is SolidColorBrush s)
                            {
                                _TextPaint.Color = s.Color.ToSKColor();
                            }
                            canvas.DrawText(textToFormat, origin.X, origin.Y, _TextPaint);

                            origin.X += (float)((CalculateAddressColumnCharWidth() + _CharsBetweenSections) * _TextMeasure.Width);
                        }
                    }

                    long savedDataSourcePositionBeforeReadingData = DataSource.BaseStream.Position;

                    if (ShowData)
                    {
                        origin.X += (float)(_CharsBetweenSections * _TextMeasure.Width);

                        var cachedDataColumnCharWidth = CalculateDataColumnCharWidth();

                        // Needed to track text in alternating columns so we can use a different brush when drawing
                        var evenColumnBuilder = new StringBuilder(Columns * DataWidth);
                        var oddColumnBuilder = new StringBuilder(Columns * DataWidth);

                        var column = 0;

                        // Draw text up until selection start point
                        while (column < Columns)
                        {
                            if (DataSource.BaseStream.Position + _BytesPerColumn <= DataSource.BaseStream.Length)
                            {
                                if (DataSource.BaseStream.Position >= SelectedOffset)
                                {
                                    break;
                                }

                                var textToFormat = ReadFormattedData();

                                if (column % 2 == 0)
                                {
                                    evenColumnBuilder.Append(textToFormat);
                                    evenColumnBuilder.Append(' ', _CharsBetweenDataColumns);

                                    oddColumnBuilder.Append(' ', textToFormat.Length + _CharsBetweenDataColumns);
                                }
                                else
                                {
                                    oddColumnBuilder.Append(textToFormat);
                                    oddColumnBuilder.Append(' ', _CharsBetweenDataColumns);

                                    evenColumnBuilder.Append(' ', textToFormat.Length + _CharsBetweenDataColumns);
                                }
                            }
                            else
                            {
                                evenColumnBuilder.Append(' ', cachedDataColumnCharWidth + _CharsBetweenDataColumns);
                                oddColumnBuilder.Append(' ', cachedDataColumnCharWidth + _CharsBetweenDataColumns);
                            }

                            ++column;
                        }

                        {
                            if (Foreground is SolidColorBrush s)
                            {
                                _TextPaint.Color = s.Color.ToSKColor();
                            }
                            DrawTextAccuracy(canvas, _TextPaint, origin, evenColumnBuilder.ToString());
                        }

                        {
                            if (AlternatingDataColumnTextBrush is SolidColorBrush s)
                            {
                                _TextPaint.Color = s.Color.ToSKColor();
                            }
                            DrawTextAccuracy(canvas, _TextPaint, origin, oddColumnBuilder.ToString());
                        }
                        origin.X += evenColumnBuilder.Length * _TextMeasure.Width;

                        if (column < Columns)
                        {
                            // We'll reuse this builder for drawing selection text
                            evenColumnBuilder.Clear();

                            // Draw text starting from selection start point
                            while (column < Columns)
                            {
                                if (DataSource.BaseStream.Position + _BytesPerColumn <= DataSource.BaseStream.Length)
                                {
                                    if (DataSource.BaseStream.Position >= SelectedOffset + SelectionLength)
                                    {
                                        break;
                                    }

                                    var textToFormat = ReadFormattedData();

                                    evenColumnBuilder.Append(textToFormat);
                                    evenColumnBuilder.Append(' ', _CharsBetweenDataColumns);
                                }
                                else
                                {
                                    evenColumnBuilder.Append(' ', cachedDataColumnCharWidth + _CharsBetweenDataColumns);
                                }

                                ++column;
                            }

                            {
                                if (SelectionTextBrush is SolidColorBrush s)
                                {
                                    _TextPaint.Color = s.Color.ToSKColor();
                                }
                                DrawTextAccuracy(canvas, _TextPaint, origin, evenColumnBuilder.ToString());
                            }

                            origin.X += evenColumnBuilder.Length * _TextMeasure.Width;

                            if (column < Columns)
                            {
                                evenColumnBuilder.Clear();
                                oddColumnBuilder.Clear();

                                // Draw text after end of selection
                                while (column < Columns)
                                {
                                    if (DataSource.BaseStream.Position + _BytesPerColumn <= DataSource.BaseStream.Length)
                                    {
                                        var textToFormat = ReadFormattedData();
                                        if (column % 2 == 0)
                                        {
                                            evenColumnBuilder.Append(textToFormat);
                                            evenColumnBuilder.Append(' ', _CharsBetweenDataColumns);

                                            oddColumnBuilder.Append(' ', textToFormat.Length + _CharsBetweenDataColumns);
                                        }
                                        else
                                        {
                                            oddColumnBuilder.Append(textToFormat);
                                            oddColumnBuilder.Append(' ', _CharsBetweenDataColumns);

                                            evenColumnBuilder.Append(' ', textToFormat.Length + _CharsBetweenDataColumns);
                                        }
                                    }
                                    else
                                    {
                                        evenColumnBuilder.Append(' ', cachedDataColumnCharWidth + _CharsBetweenDataColumns);
                                        oddColumnBuilder.Append(' ', cachedDataColumnCharWidth + _CharsBetweenDataColumns);
                                    }

                                    ++column;
                                }

                                {
                                    if (Foreground is SolidColorBrush s)
                                    {
                                        _TextPaint.Color = s.Color.ToSKColor();
                                    }
                                    DrawTextAccuracy(canvas, _TextPaint, origin, evenColumnBuilder.ToString());
                                }

                                {
                                    if (AlternatingDataColumnTextBrush is SolidColorBrush s)
                                    {
                                        _TextPaint.Color = s.Color.ToSKColor();
                                    }
                                    DrawTextAccuracy(canvas, _TextPaint, origin, oddColumnBuilder.ToString());
                                }

                                origin.X += oddColumnBuilder.Length * _TextMeasure.Width;
                            }
                        }

                        // Compensate for the extra space added at the end of the builder
                        origin.X += (float)((_CharsBetweenSections - _CharsBetweenDataColumns) * _TextMeasure.Width);
                    }

                    if (ShowText)
                    {
                        origin.X += (float)(_CharsBetweenSections * _TextMeasure.Width);

                        if (ShowData)
                        {
                            // Reset the stream to read one byte at a time
                            DataSource.BaseStream.Position = savedDataSourcePositionBeforeReadingData;
                        }

                        var builder = new StringBuilder(Columns * DataWidth);

                        var column = 0;

                        // Draw text up until selection start point
                        while (column < Columns)
                        {
                            if (DataSource.BaseStream.Position + _BytesPerColumn <= DataSource.BaseStream.Length)
                            {
                                if (DataSource.BaseStream.Position >= SelectedOffset)
                                {
                                    break;
                                }

                                var textToFormat = ReadFormattedText();
                                builder.Append(textToFormat);
                            }

                            ++column;
                        }

                        {
                            if (Foreground is SolidColorBrush s)
                            {
                                _TextPaint.Color = s.Color.ToSKColor();
                            }
                            DrawTextAccuracy(canvas, _TextPaint, origin, builder.ToString());
                        }

                        if (column < Columns)
                        {
                            origin.X += builder.Length * _TextMeasure.Width;

                            builder.Clear();

                            // Draw text starting from selection start point
                            while (column < Columns)
                            {
                                if (DataSource.BaseStream.Position + _BytesPerColumn <= DataSource.BaseStream.Length)
                                {
                                    if (DataSource.BaseStream.Position >= SelectedOffset + SelectionLength)
                                    {
                                        break;
                                    }

                                    var textToFormat = ReadFormattedText();
                                    builder.Append(textToFormat);
                                }

                                ++column;
                            }

                            {
                                if (SelectionTextBrush is SolidColorBrush s)
                                {
                                    _TextPaint.Color = s.Color.ToSKColor();
                                }

                                DrawTextAccuracy(canvas, _TextPaint, origin, builder.ToString());
                            }

                            if (column < Columns)
                            {
                                origin.X += builder.Length * _TextMeasure.Width;

                                builder.Clear();

                                // Draw text after end of selection
                                while (column < Columns)
                                {
                                    if (DataSource.BaseStream.Position + _BytesPerColumn <= DataSource.BaseStream.Length)
                                    {
                                        var textToFormat = ReadFormattedText();
                                        builder.Append(textToFormat);
                                    }

                                    ++column;
                                }

                                {
                                    if (Foreground is SolidColorBrush s)
                                    {
                                        _TextPaint.Color = s.Color.ToSKColor();
                                    }

                                    DrawTextAccuracy(canvas, _TextPaint, origin, builder.ToString());
                                }
                            }
                        }
                    }

                    origin.X = 0;
                    origin.Y += _TextMeasure.Height;
                }

                DataSource.BaseStream.Position = savedDataSourcePosition;
            }
        }

        /// <summary>
        /// Scrolls the contents of the control to the specified offset.
        /// </summary>
        ///
        /// <param name="offset">
        /// The offset to scroll to.
        /// </param>
        public void ScrollToOffset(long offset)
        {
            long maxBytesDisplayed = _BytesPerRow * MaxVisibleRows;
            long lastByteOffset = (DataSource?.BaseStream?.Length ?? 1) - 1;

            // Adjust requested offset if not existing
            if (offset < 0)
            {
                offset = 0;
            }
            else if (offset > lastByteOffset)
            {
                offset = lastByteOffset;
            }

            if (Offset > offset)
            {
                // We need to scroll up
                Offset -= ((Offset - offset - 1) / _BytesPerRow + 1) * _BytesPerRow;
            }

            if (Offset + maxBytesDisplayed <= offset)
            {
                // We need to scroll down
                Offset += ((offset - (Offset + maxBytesDisplayed)) / _BytesPerRow + 1) * _BytesPerRow;
            }
        }

        // Using .HasFlag(x) to correctly detect state of modifier keys (CTRL, SHIFT, ...)
        private static bool IsKeyDown(VirtualKey key) => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            base.OnKeyDown(e);

            // Context Menu
            switch (e.Key)
            {
                case VirtualKey.Application:
                    {
                        ShowContextMenu();
                        e.Handled = true;
                        return;
                    }

                case VirtualKey.F10:
                    {
                        if (IsKeyDown(VirtualKey.LeftShift) || IsKeyDown(VirtualKey.RightShift))
                        {
                            ShowContextMenu();
                        }

                        e.Handled = true;
                        return;
                    }
            }

            // Other keys
            if (Columns > 0 && MaxVisibleRows > 0)
            {
                switch (e.Key)
                {
                    case VirtualKey.A:
                        {
                            if (IsKeyDown(VirtualKey.LeftControl) || IsKeyDown(VirtualKey.RightControl))
                            {
                                if (SelectAllCanExecute(null))
                                {
                                    SelectionStart = 0;
                                    SelectionEnd = DataSource.BaseStream.Length;
                                }
                            }

                            e.Handled = true;
                            break;
                        }

                    case VirtualKey.C:
                        {
                            if (IsKeyDown(VirtualKey.LeftControl) || IsKeyDown(VirtualKey.RightControl))
                            {
                                if (CopyCanExecute(null))
                                {
                                    if (IsKeyDown(VirtualKey.LeftShift) || IsKeyDown(VirtualKey.RightShift))
                                    {
                                        // Copy text
                                        Copy(true);
                                    }
                                    else
                                    {
                                        // Copy data
                                        Copy(false);
                                    }
                                }
                            }

                            e.Handled = true;
                            break;
                        }

                    case VirtualKey.Down:
                        {
                            if (IsKeyDown(VirtualKey.LeftShift) || IsKeyDown(VirtualKey.RightShift))
                            {
                                SelectionEnd += _BytesPerRow;
                            }
                            else
                            {
                                SelectionStart += _BytesPerRow;
                                SelectionEnd = SelectionStart + _BytesPerColumn;
                            }

                            ScrollToOffset(SelectionEnd - _BytesPerColumn);

                            e.Handled = true;

                            break;
                        }

                    case VirtualKey.End:
                        {
                            if (IsKeyDown(VirtualKey.LeftControl) || IsKeyDown(VirtualKey.RightControl))
                            {
                                SelectionEnd = DataSource.BaseStream.Length;

                                if (!IsKeyDown(VirtualKey.LeftShift) && !IsKeyDown(VirtualKey.RightShift))
                                {
                                    SelectionStart = SelectionEnd - _BytesPerColumn;
                                }

                                ScrollToOffset(SelectionEnd - _BytesPerColumn);
                            }
                            else
                            {
                                SelectionEnd += (Offset - SelectionEnd).Mod(_BytesPerRow);

                                if (!IsKeyDown(VirtualKey.LeftShift) && !IsKeyDown(VirtualKey.RightShift))
                                {
                                    SelectionStart = SelectionEnd - _BytesPerColumn;
                                }

                                ScrollToOffset(SelectionEnd - _BytesPerColumn);
                            }

                            e.Handled = true;

                            break;
                        }

                    case VirtualKey.Home:
                        {
                            if (IsKeyDown(VirtualKey.LeftControl) || IsKeyDown(VirtualKey.RightControl))
                            {
                                SelectionEnd = 0;

                                if (!IsKeyDown(VirtualKey.LeftShift) && !IsKeyDown(VirtualKey.RightShift))
                                {
                                    SelectionStart = SelectionEnd;
                                    SelectionEnd = SelectionStart + _BytesPerColumn;
                                }

                                ScrollToOffset(SelectionEnd - _BytesPerColumn);
                            }
                            else
                            {
                                // TODO: Because of the way we represent selection there is no way to distinguish at the
                                // moment whether the selection ends at the start of the current line or the end of the
                                // previous line. As such, when the Shift+End hotkey is used twice consecutively a whole
                                // new line above the current selection will be selected. This is undesirable behavior
                                // that deviates from the canonical semantics of Shift+End.
                                SelectionEnd -= (SelectionEnd - 1 - Offset).Mod(_BytesPerRow) + 1;

                                if (!IsKeyDown(VirtualKey.LeftShift) && !IsKeyDown(VirtualKey.RightShift))
                                {
                                    SelectionStart = SelectionEnd;
                                    SelectionEnd = SelectionStart + _BytesPerColumn;
                                }

                                ScrollToOffset(SelectionEnd - _BytesPerColumn);
                            }

                            e.Handled = true;

                            break;
                        }

                    case VirtualKey.Left:
                        {
                            if (IsKeyDown(VirtualKey.LeftShift) || IsKeyDown(VirtualKey.RightShift))
                            {
                                SelectionEnd -= _BytesPerColumn;
                            }
                            else
                            {
                                SelectionStart -= _BytesPerColumn;
                                SelectionEnd = SelectionStart + _BytesPerColumn;
                            }

                            ScrollToOffset(SelectionEnd - _BytesPerColumn);

                            e.Handled = true;

                            break;
                        }

                    case VirtualKey.PageDown:
                        {
                            bool isOffsetVisibleBeforeSelectionChange = IsOffsetVisible(SelectionEnd);

                            SelectionEnd += _BytesPerRow * MaxVisibleRows;

                            if (!IsKeyDown(VirtualKey.LeftShift) && !IsKeyDown(VirtualKey.RightShift))
                            {
                                SelectionStart = SelectionEnd - _BytesPerColumn;
                            }

                            _ScrollBar.Value += MaxVisibleRows;

                            OnVerticalScrollBarScroll(_ScrollBar, ScrollEventType.SmallIncrement, _ScrollBar.Value);

                            e.Handled = true;
                            break;
                        }

                    case VirtualKey.PageUp:
                        {
                            bool isOffsetVisibleBeforeSelectionChange = IsOffsetVisible(SelectionEnd);

                            SelectionEnd -= _BytesPerRow * MaxVisibleRows;

                            if (!IsKeyDown(VirtualKey.LeftShift) && !IsKeyDown(VirtualKey.RightShift))
                            {
                                SelectionStart = SelectionEnd - _BytesPerColumn;
                                SelectionEnd = SelectionStart + _BytesPerColumn;
                            }

                            _ScrollBar.Value -= MaxVisibleRows;

                            OnVerticalScrollBarScroll(_ScrollBar, ScrollEventType.SmallIncrement, _ScrollBar.Value);

                            e.Handled = true;
                            break;
                        }

                    case VirtualKey.Right:
                        {
                            if (IsKeyDown(VirtualKey.LeftShift) || IsKeyDown(VirtualKey.RightShift))
                            {
                                SelectionEnd += _BytesPerColumn;
                            }
                            else
                            {
                                SelectionStart += _BytesPerColumn;
                                SelectionEnd = SelectionStart + _BytesPerColumn;
                            }

                            ScrollToOffset(SelectionEnd - _BytesPerColumn);

                            e.Handled = true;
                            break;
                        }

                    case VirtualKey.Up:
                        {
                            if (IsKeyDown(VirtualKey.LeftShift) || IsKeyDown(VirtualKey.RightShift))
                            {
                                SelectionEnd -= _BytesPerRow;
                            }
                            else
                            {
                                SelectionStart -= _BytesPerRow;
                                SelectionEnd = SelectionStart + _BytesPerColumn;
                            }

                            ScrollToOffset(SelectionEnd - _BytesPerColumn);

                            e.Handled = true;
                            break;
                        }
                }
            }
        }

        protected override void OnDoubleTapped(DoubleTappedRoutedEventArgs e)
        {
            Focus(FocusState.Programmatic);
            e.Handled = true;

            if (e.PointerDeviceType == PointerDeviceType.Mouse)
            {
                OnMouseDoubleClick(e.GetPosition(_Canvas));
            }
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            Focus(FocusState.Programmatic);
            e.Handled = true;

            var pps = e.GetCurrentPoint(this).Properties;
            if (pps != null)
            {
                if (pps.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
                {
                    OnMouseLeftButtonDown(e);
                }
            }
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);
            var pps = e.GetCurrentPoint(this).Properties;
            if (pps != null)
            {
                if (pps.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
                {
                    OnMouseLeftButtonUp(e);
                }
            }
        }

        protected override void OnPointerCanceled(PointerRoutedEventArgs e)
        {
            base.OnPointerCanceled(e);
            var pps = e.GetCurrentPoint(this).Properties;
            if (pps != null)
            {
                if (pps.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
                {
                    OnMouseLeftButtonUp(e);
                }
            }
        }

        protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
        {
            base.OnPointerCaptureLost(e);
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);
        }

        /// <inheritdoc/>
        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_HighlightState != SelectionArea.None)
            {
                var position = e.GetCurrentPoint(_Canvas).Position;
                var currentMouseOverOffset = ConvertPositionToOffset(position);

                switch (_HighlightState)
                {
                    case SelectionArea.Address:
                        {
                            if (currentMouseOverOffset >= SelectionStart)
                            {
                                SelectionEnd = currentMouseOverOffset + _BytesPerRow;
                            }
                            else
                            {
                                SelectionEnd = currentMouseOverOffset;
                            }

                            // Adjust start point
                            if (SelectionStart > SelectionEnd && _pointerMoveSelectionAdjustment != SelectionAdjustment.Up)
                            {
                                // If moving up and SelectionStart was previously adjusted down or not adjusted, then set SelectionStart to end of row.
                                SelectionStart = SelectionStart + (_BytesPerRow - _BytesPerColumn);
                                _pointerMoveSelectionAdjustment = SelectionAdjustment.Up;
                            }
                            else if (SelectionStart < SelectionEnd && _pointerMoveSelectionAdjustment == SelectionAdjustment.Up)
                            {
                                // If moving down and SelectionStart was previously adjusted up, then set SelectionStart to start of row.
                                SelectionStart = SelectionStart - (_BytesPerRow - _BytesPerColumn);
                                _pointerMoveSelectionAdjustment = SelectionAdjustment.Down;
                            }
                            break;
                        }
                    case SelectionArea.Data:
                    case SelectionArea.Text:
                        {
                            if (currentMouseOverOffset >= SelectionStart)
                            {
                                SelectionEnd = currentMouseOverOffset + _BytesPerColumn;
                            }
                            else
                            {
                                SelectionEnd = currentMouseOverOffset;
                            }
                            break;
                        }
                }

                // Move next row into view if selection goes out of view
                if (position.Y > _AddressRect.Y + _AddressRect.Height)
                {
                    ScrollToOffset(currentMouseOverOffset + _BytesPerRow);
                }
                else if (position.Y < _AddressRect.Y)
                {
                    ScrollToOffset(currentMouseOverOffset - _BytesPerRow);
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            var Delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;

            var value = _ScrollBar.Value;
            if (Delta < 0)
            {
                _ScrollBar.Value += _ScrollWheelScrollRows;

                OnVerticalScrollBarScroll(_ScrollBar, ScrollEventType.SmallIncrement, _ScrollBar.Value);
            }
            else
            {
                _ScrollBar.Value -= _ScrollWheelScrollRows;

                OnVerticalScrollBarScroll(_ScrollBar, ScrollEventType.SmallDecrement, _ScrollBar.Value);
            }
        }

        /// <inheritdoc/>
        private void OnMouseDoubleClick(Point position)
        {
            Point addressVerticalLinePoint0 = CalculateAddressVerticalLinePoint0();

            if (position.X < addressVerticalLinePoint0.X)
            {
                _HighlightBegin = SelectionArea.Address;
                _HighlightState = SelectionArea.Address;

                SelectionStart = ConvertPositionToOffset(position);
                SelectionEnd = SelectionStart + _BytesPerRow;
            }
        }

        /// <inheritdoc/>
        private void OnMouseLeftButtonDown(PointerRoutedEventArgs e)
        {
            if (_HighlightState == SelectionArea.None && CapturePointer(e.Pointer))
            {
                Point position = e.GetCurrentPoint(_Canvas).Position;

                Point addressVerticalLinePoint0 = CalculateAddressVerticalLinePoint0();
                Point dataVerticalLinePoint0 = CalculateDataVerticalLinePoint0();
                Point textVerticalLinePoint0 = CalculateTextVerticalLinePoint0();

                if (position.X < addressVerticalLinePoint0.X)
                {
                    _HighlightBegin = SelectionArea.Address;
                    _HighlightState = SelectionArea.Address;
                }
                else if (position.X < dataVerticalLinePoint0.X)
                {
                    _HighlightBegin = SelectionArea.Data;
                    _HighlightState = SelectionArea.Data;
                }
                else if (position.X < textVerticalLinePoint0.X)
                {
                    _HighlightBegin = SelectionArea.Text;
                    _HighlightState = SelectionArea.Text;
                }

                if (_HighlightState != SelectionArea.None)
                {
                    SelectionStart = ConvertPositionToOffset(position);

                    SelectionEnd = SelectionStart + _BytesPerColumn;
                }
            }
        }

        /// <inheritdoc/>
        private void OnMouseLeftButtonUp(PointerRoutedEventArgs e)
        {
            _HighlightState = SelectionArea.None;

            ReleasePointerCapture(e.Pointer);
        }

        private static void OnPropertyChangedInvalidateVisual(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var HexBox = (HexBox)d;

            HexBox.Reflush();
        }

        private static void OnSelectionEndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var HexBox = (HexBox)d;

            HexBox.Reflush();

            HexBox.OnPropertyChanged(nameof(SelectionEnd));
            HexBox.OnPropertyChanged(nameof(SelectionLength));
            HexBox.OnPropertyChanged(nameof(SelectedOffset));
            HexBox.OnPropertyChanged(nameof(SelectedAddress));
            HexBox.OnPropertyChanged(nameof(IsSelectionActive));
        }

        private static void OnSelectionStartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var HexBox = (HexBox)d;

            HexBox.Reflush();

            HexBox.OnPropertyChanged(nameof(SelectionStart));
            HexBox.OnPropertyChanged(nameof(SelectionLength));
            HexBox.OnPropertyChanged(nameof(SelectedOffset));
            HexBox.OnPropertyChanged(nameof(SelectedAddress));
            HexBox.OnPropertyChanged(nameof(IsSelectionActive));
        }

        private static object CoerceColumns(DependencyObject d, object value)
        {
            var HexBox = (HexBox)d;

            if (HexBox.MaxVisibleColumns == 0)
            {
                return (int)value;
            }
            else
            {
                return Math.Min((int)value, HexBox.MaxVisibleColumns);
            }
        }

        private static object CoerceMaxVisibleColumns(DependencyObject d, object value)
        {
            return Math.Min((int)value, _MaxColumns);
        }

        private static object CoerceMaxVisibleRows(DependencyObject d, object value)
        {
            return Math.Min((int)value, _MaxRows);
        }

        private static object CoerceSelectionStart(DependencyObject d, object value)
        {
            var HexBox = (HexBox)d;

            if (HexBox.DataSource != null)
            {
                long selectionStart = (long)value;

                // Selection offset cannot start in the middle of the data width
                selectionStart -= selectionStart % HexBox._BytesPerColumn;

                // Selection start cannot be at the end of the stream so adjust by data width number of bytes
                value = selectionStart.Clamp(0, HexBox.DataSource.BaseStream.Length / HexBox._BytesPerColumn * HexBox._BytesPerColumn - HexBox._BytesPerColumn);
            }
            else
            {
                value = 0L;
            }

            return value;
        }

        private static object CoerceSelectionEnd(DependencyObject d, object value)
        {
            var HexBox = (HexBox)d;

            if (HexBox.DataSource != null)
            {
                long selectionEnd = (long)value;

                // Selection offset cannot start in the middle of the data width
                selectionEnd -= selectionEnd % HexBox._BytesPerColumn;

                // Unlike selection start the selection end can be at the end of the stream
                value = selectionEnd.Clamp(0, HexBox.DataSource.BaseStream.Length / HexBox._BytesPerColumn * HexBox._BytesPerColumn);
            }
            else
            {
                value = 0L;
            }

            return value;
        }

        private static object CoerceOffset(DependencyObject d, object value)
        {
            var HexBox = (HexBox)d;

            if (HexBox.DataSource != null)
            {
                long offset = (long)value;

                value = offset.Clamp(0, HexBox.DataSource.BaseStream.Length);
            }
            else
            {
                value = 0L;
            }

            return value;
        }

        private static void OnAddressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var HexBox = (HexBox)d;

            HexBox.SelectionStart = 0;
            HexBox.SelectionEnd = 0;

            HexBox.Reflush();

            HexBox.OnPropertyChanged(nameof(Address));
            HexBox.OnPropertyChanged(nameof(SelectedAddress));
        }

        private static void OnDataTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var HexBox = (HexBox)d;

            switch (HexBox.DataType)
            {
                case DataType.Int_1:
                    HexBox.DataWidth = 1;
                    break;
                case DataType.Int_2:
                    HexBox.DataWidth = 2;
                    break;
                case DataType.Int_4:
                    HexBox.DataWidth = 4;
                    break;
                case DataType.Int_8:
                    HexBox.DataWidth = 8;
                    break;
                case DataType.Float_32:
                    HexBox.DataWidth = 4;
                    break;
                case DataType.Float_64:
                    HexBox.DataWidth = 8;
                    break;
            }

            HexBox.Reflush();
        }

        private static void OnDataSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var HexBox = (HexBox)d;

            HexBox.Offset = 0;
            HexBox.SelectionStart = 0;
            HexBox.SelectionEnd = 0;

            HexBox.Reflush();
        }

        private void Reflush()
        {
            if (_Canvas != null)
            {
                _Canvas.Invalidate();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private string ReadFormattedText()
        {
            StringBuilder builder = new(DataWidth);

            switch (TextFormat)
            {
                case TextFormat.Ascii:
                    {
                        for (var k = 0; k < DataWidth; ++k)
                        {
                            byte value = DataSource.ReadByte();

                            if (value > 31 && value < 127)
                            {
                                builder.Append(Convert.ToChar(value));
                            }
                            else
                            {
                                builder.Append('.');
                            }
                        }

                        break;
                    }

                default:
                    {
                        throw new InvalidOperationException($"Invalid {nameof(TextFormat)} value.");
                    }
            }

            return builder.ToString();
        }

        private string ReadFormattedData()
        {
            string result;

            if (DataType < DataType.Float_32)
            {
                switch (DataFormat)
                {
                    case DataFormat.Decimal:
                        {
                            if (DataSignedness == DataSignedness.Signed)
                            {
                                switch (DataType)
                                {
                                    case DataType.Int_1:
                                        {
                                            result = $"{DataSource.ReadSByte():+#;-#;0}".PadLeft(4);
                                            break;
                                        }

                                    case DataType.Int_2:
                                        {
                                            result = $"{EndianBitConverter.Convert(DataSource.ReadInt16(), Endianness):+#;-#;0}".PadLeft(6);
                                            break;
                                        }

                                    case DataType.Int_4:
                                        {
                                            result = $"{EndianBitConverter.Convert(DataSource.ReadInt32(), Endianness):+#;-#;0}".PadLeft(11);
                                            break;
                                        }

                                    case DataType.Int_8:
                                        {
                                            result = $"{EndianBitConverter.Convert(DataSource.ReadInt64(), Endianness):+#;-#;0}".PadLeft(21);
                                            break;
                                        }

                                    default:
                                        {
                                            throw new InvalidOperationException($"Invalid {nameof(DataWidth)} value.");
                                        }
                                }
                            }
                            else if (DataSignedness == DataSignedness.Unsigned)
                            {
                                switch (DataType)
                                {
                                    case DataType.Int_1:
                                        {
                                            result = $"{DataSource.ReadByte()}".PadLeft(3);
                                            break;
                                        }

                                    case DataType.Int_2:
                                        {
                                            result = $"{EndianBitConverter.Convert(DataSource.ReadUInt16(), Endianness)}".PadLeft(5);
                                            break;
                                        }

                                    case DataType.Int_4:
                                        {
                                            result = $"{EndianBitConverter.Convert(DataSource.ReadUInt32(), Endianness)}".PadLeft(10);
                                            break;
                                        }

                                    case DataType.Int_8:
                                        {
                                            result = $"{EndianBitConverter.Convert(DataSource.ReadUInt64(), Endianness)}".PadLeft(20);
                                            break;
                                        }

                                    default:
                                        {
                                            throw new InvalidOperationException($"Invalid {nameof(DataWidth)} value.");
                                        }
                                }
                            }
                            else
                            {
                                throw new InvalidOperationException($"Invalid {nameof(DataType)} value.");
                            }
                        }
                        break;

                    case DataFormat.Hexadecimal:
                        {
                            switch (DataType)
                            {
                                case DataType.Int_1:
                                    {
                                        result = $"{DataSource.ReadByte(),0:X2}";
                                        break;
                                    }

                                case DataType.Int_2:
                                    {
                                        result = $"{EndianBitConverter.Convert(DataSource.ReadUInt16(), Endianness),0:X4}";
                                        break;
                                    }

                                case DataType.Int_4:
                                    {
                                        result = $"{EndianBitConverter.Convert(DataSource.ReadUInt32(), Endianness),0:X8}";
                                        break;
                                    }

                                case DataType.Int_8:
                                    {
                                        result = $"{EndianBitConverter.Convert(DataSource.ReadUInt64(), Endianness),0:X16}";
                                        break;
                                    }

                                default:
                                    {
                                        throw new InvalidOperationException($"Invalid {nameof(DataWidth)} value.");
                                    }
                            }

                            break;
                        }

                    default:
                        {
                            throw new InvalidOperationException($"Invalid {nameof(DataFormat)} value.");
                        }
                }
            }
            else
            {
                switch (DataType)
                {
                    case DataType.Float_32:
                        {
                            var bytes = BitConverter.GetBytes(EndianBitConverter.Convert(DataSource.ReadUInt32(), Endianness));
                            var value = BitConverter.ToSingle(bytes, 0);
                            result = $"{value:E08}".PadLeft(16);
                            break;
                        }

                    case DataType.Float_64:
                        {
                            var bytes = BitConverter.GetBytes(EndianBitConverter.Convert(DataSource.ReadUInt64(), Endianness));
                            var value = BitConverter.ToSingle(bytes, 0);
                            result = $"{value:E16}".PadLeft(24);
                            break;
                        }

                    default:
                        {
                            throw new InvalidOperationException($"Invalid {nameof(DataWidth)} value.");
                        }
                }
            }

            return result;
        }

        private void SelectAllExecuted(object sender)
        {
            SelectAll();
        }

        private void CopyExecuted(object sender)
        {
            Copy(false);
        }

        private void CopyTextExecuted(object sender)
        {
            Copy(true);
        }

        private bool SelectAllCanExecute(object sender)
        {
            return DataSource != null && (ShowData || ShowText);
        }

        private bool CopyCanExecute(object sender)
        {
            return IsSelectionActive && (ShowData || ShowText);
        }

        private void OnVerticalScrollBarValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            _LastVerticalScrollValue = e.OldValue;
        }

        private void OnVerticalScrollBarScroll(object sender, ScrollEventArgs e)
        {
            long newOffset = (long)e.NewValue * _BytesPerRow;

            Offset = newOffset;
        }

        private void OnVerticalScrollBarScroll(object sender, ScrollEventType type, double NewValue)
        {
            long newOffset = (long)NewValue * _BytesPerRow;

            Offset = newOffset;
        }

        private string GetFormattedAddressText(ulong address)
        {
            string formattedAddressText;

            switch (AddressFormat)
            {
                case AddressFormat.Address16:
                    {
                        formattedAddressText = $"{address & 0xFFFF,0:X4}";
                        break;
                    }

                case AddressFormat.Address24:
                    {
                        formattedAddressText = $"{address >> 16 & 0xFF,0:X2}:{address & 0xFFFF,0:X4}";
                        break;
                    }

                case AddressFormat.Address32:
                    {
                        formattedAddressText = $"{address >> 16 & 0xFFFF,0:X4}:{address & 0xFFFF,0:X4}";
                        break;
                    }

                case AddressFormat.Address48:
                    {
                        formattedAddressText = $"{address >> 32 & 0xFF,0:X4}:{address & 0xFFFFFFFF,0:X8}";
                        break;
                    }

                case AddressFormat.Address64:
                    {
                        formattedAddressText = $"{address >> 32,0:X8}:{address & 0xFFFFFFFF,0:X8}";
                        break;
                    }

                default:
                    {
                        throw new InvalidOperationException($"Invalid {nameof(AddressFormat)} value.");
                    }
            }

            return formattedAddressText;
        }

        private int CalculateAddressColumnCharWidth()
        {
            int addressColumnCharWidth;

            switch (AddressFormat)
            {
                case AddressFormat.Address16:
                    {
                        addressColumnCharWidth = 4;
                        break;
                    }

                case AddressFormat.Address24:
                    {
                        addressColumnCharWidth = 7;
                        break;
                    }

                case AddressFormat.Address32:
                    {
                        addressColumnCharWidth = 9;
                        break;
                    }

                case AddressFormat.Address48:
                    {
                        addressColumnCharWidth = 13;
                        break;
                    }

                case AddressFormat.Address64:
                    {
                        addressColumnCharWidth = 17;
                        break;
                    }

                default:
                    {
                        throw new InvalidOperationException($"Invalid {nameof(AddressFormat)} value.");
                    }
            }

            return addressColumnCharWidth;
        }

        private int CalculateDataColumnCharWidth()
        {
            int dataColumnCharWidth;

            if (DataType < DataType.Float_32)
            {
                switch (DataFormat)
                {
                    case DataFormat.Decimal:
                        {
                            switch (DataSignedness)
                            {
                                case DataSignedness.Signed:
                                    {
                                        switch (DataType)
                                        {
                                            case DataType.Int_1:
                                                {
                                                    dataColumnCharWidth = 4;
                                                    break;
                                                }

                                            case DataType.Int_2:
                                                {
                                                    dataColumnCharWidth = 6;
                                                    break;
                                                }

                                            case DataType.Int_4:
                                                {
                                                    dataColumnCharWidth = 11;
                                                    break;
                                                }

                                            case DataType.Int_8:
                                                {
                                                    dataColumnCharWidth = 21;
                                                    break;
                                                }

                                            default:
                                                {
                                                    throw new InvalidOperationException($"Invalid {nameof(DataWidth)} value.");
                                                }
                                        }
                                    }

                                    break;

                                case DataSignedness.Unsigned:
                                    {
                                        switch (DataType)
                                        {
                                            case DataType.Int_1:
                                                {
                                                    dataColumnCharWidth = 3;
                                                    break;
                                                }

                                            case DataType.Int_2:
                                                {
                                                    dataColumnCharWidth = 5;
                                                    break;
                                                }

                                            case DataType.Int_4:
                                                {
                                                    dataColumnCharWidth = 10;
                                                    break;
                                                }

                                            case DataType.Int_8:
                                                {
                                                    dataColumnCharWidth = 20;
                                                    break;
                                                }

                                            default:
                                                {
                                                    throw new InvalidOperationException($"Invalid {nameof(DataWidth)} value.");
                                                }
                                        }
                                    }

                                    break;

                                default:
                                    {
                                        throw new InvalidOperationException($"Invalid {nameof(DataType)} value.");
                                    }
                            }
                        }

                        break;

                    case DataFormat.Hexadecimal:
                        {
                            switch (DataWidth)
                            {
                                case 1:
                                case 2:
                                case 4:
                                case 8:
                                    {
                                        dataColumnCharWidth = 2 * DataWidth;
                                        break;
                                    }

                                default:
                                    {
                                        throw new InvalidOperationException($"Invalid {nameof(DataWidth)} value.");
                                    }
                            }

                            break;
                        }

                    default:
                        {
                            throw new InvalidOperationException($"Invalid {nameof(DataFormat)} value.");
                        }
                }
            }
            else
            {
                switch (DataType)
                {
                    case DataType.Float_32:
                        {
                            dataColumnCharWidth = 16;
                            break;
                        }

                    case DataType.Float_64:
                        {
                            dataColumnCharWidth = 24;
                            break;
                        }

                    default:
                        {
                            throw new InvalidOperationException($"Invalid {nameof(DataWidth)} value.");
                        }
                }
            }
            return dataColumnCharWidth;
        }

        private Point CalculateAddressVerticalLinePoint0()
        {
            Point point1 = default;

            if (ShowAddress)
            {
                point1.X = (CalculateAddressColumnCharWidth() + _CharsBetweenSections) * _TextMeasure.Width;
            }

            return point1;
        }

        private Point CalculateAddressVerticalLinePoint1()
        {
            Point point2 = default;

            if (ShowAddress)
            {
                point2.X = (CalculateAddressColumnCharWidth() + _CharsBetweenSections) * _TextMeasure.Width;
            }

            point2.Y = Math.Min(_TextMeasure.Height * (MaxVisibleRows + 1), _Canvas.ActualHeight);

            return point2;
        }

        private Point CalculateDataVerticalLinePoint0()
        {
            Point point1 = CalculateAddressVerticalLinePoint0();

            if (ShowData)
            {
                point1.X += (_CharsBetweenSections + (CalculateDataColumnCharWidth() + _CharsBetweenDataColumns) * Columns - _CharsBetweenDataColumns + _CharsBetweenSections) * _TextMeasure.Width;
            }

            return point1;
        }

        private Point CalculateDataVerticalLinePoint1()
        {
            Point point2 = CalculateAddressVerticalLinePoint1();

            if (ShowData)
            {
                point2.X += (_CharsBetweenSections + (CalculateDataColumnCharWidth() + _CharsBetweenDataColumns) * Columns - _CharsBetweenDataColumns + _CharsBetweenSections) * _TextMeasure.Width;
            }

            return point2;
        }

        private int CalculateTextColumnCharWidth()
        {
            return _BytesPerColumn;
        }

        private Point CalculateTextVerticalLinePoint0()
        {
            Point point1 = CalculateDataVerticalLinePoint0();

            if (ShowText)
            {
                point1.X += (_CharsBetweenSections + CalculateTextColumnCharWidth() * Columns + _CharsBetweenSections) * _TextMeasure.Width;
            }

            return point1;
        }

        private Point CalculateTextVerticalLinePoint1()
        {
            Point point2 = CalculateDataVerticalLinePoint1();

            if (ShowText)
            {
                point2.X += (_CharsBetweenSections + CalculateTextColumnCharWidth() * Columns + _CharsBetweenSections) * _TextMeasure.Width;
            }

            return point2;
        }

        private void UpdateState()
        {
            UpdateMaxVisibleRowsAndColumns();
            UpdateScrollBar();
            UpdateColumnsLayout();
        }

        private void UpdateColumnsLayout()
        {
            var p0 = CalculateAddressVerticalLinePoint0();
            var p1 = CalculateAddressVerticalLinePoint1();
            _AddressRect = new(p0, p1);

            p0 = CalculateDataVerticalLinePoint0();
            p1 = CalculateDataVerticalLinePoint1();
            _DataRect = new(p0, p1);

            p0 = CalculateTextVerticalLinePoint0();
            p1 = CalculateTextVerticalLinePoint1();
            _TextRect = new(p0, p1);
        }

        private void UpdateMaxVisibleRowsAndColumns()
        {
            int maxVisibleRows = 0;
            int maxVisibleColumns = 0;

            if ((ShowAddress || ShowData || ShowText) && _Canvas != null)
            {
                {
                    SKRect cellSize = new();
                    string bigChars = "0123456789abcdef ABCDEF";
                    for (int i = 0; i < bigChars.Length; i++)
                    {
                        var s = bigChars.Substring(i, 1);
                        if (_TextPaint.ContainsGlyphs(s))    // if the font does not contain the glyph, then skip it
                        {
                            var rect = new SKRect();
                            _TextPaint.MeasureText(s, ref rect);
                            cellSize.Union(rect);
                        }
                    }
                    _TextMeasure = cellSize;
                }

                _TextMeasure.Bottom = _TextMeasure.Height; /* 2 * line font height */

                maxVisibleRows = Math.Max(0, (int)(_Canvas.ActualHeight / _TextMeasure.Height));

                if (ShowData || ShowText)
                {
                    int charsPerRow = (int)(_Canvas.ActualWidth / _TextMeasure.Width);

                    if (ShowAddress)
                    {
                        charsPerRow -= CalculateAddressColumnCharWidth() + 2 * _CharsBetweenSections;
                    }

                    if (ShowData && ShowText)
                    {
                        charsPerRow -= 3 * _CharsBetweenSections;
                    }

                    int charsPerColumn = 0;

                    if (ShowData)
                    {
                        charsPerColumn += CalculateDataColumnCharWidth() + _CharsBetweenDataColumns;
                    }

                    if (ShowText)
                    {
                        charsPerColumn += CalculateTextColumnCharWidth();
                    }

                    if (charsPerColumn != 0)
                    {
                        maxVisibleColumns = Math.Max(0, charsPerRow / charsPerColumn);
                    }
                }
                else
                {
                    maxVisibleColumns = 0;
                }
            }

            MaxVisibleRows = maxVisibleRows;
            MaxVisibleColumns = maxVisibleColumns;

            // Maximum visible rows has now changed and so we must update the maximum amount we should scroll by
            _ScrollBar.LargeChange = maxVisibleRows;
        }

        private void UpdateScrollBar()
        {
            if ((ShowAddress || ShowData || ShowText) && DataSource != null && Columns > 0 && MaxVisibleRows > 0)
            {
                long q = DataSource.BaseStream.Length / _BytesPerRow;
                long r = DataSource.BaseStream.Length % _BytesPerRow;

                // Each scroll value represents a single drawn row
                _ScrollBar.Maximum = q + (r > 0 ? 1 : 0) - MaxVisibleRows;

                // Adjust the scroll value based on the current offset
                _ScrollBar.Value = Offset / _BytesPerRow;

                // Adjust again to compensate for residual bytes if the number of bytes between the start of the stream
                // and the current offset is less than the number of bytes we can display per row
                if (_ScrollBar.Value == 0 && Offset > 0)
                {
                    ++_ScrollBar.Value;
                }
            }
            else
            {
                _ScrollBar.Maximum = 0;
            }
        }

        private long ConvertPositionToOffset(Point position)
        {
            long offset = Offset;

            switch (_HighlightBegin)
            {
                case SelectionArea.Address:
                    {
                        // Clamp the Y coordinate to within the address region
                        position.Y = position.Y.Clamp(_AddressRect.Top, _AddressRect.Bottom);

                        // Convert the Y coordinate to the row number
                        position.Y /= _TextMeasure.Height;

                        if (position.Y >= MaxVisibleRows)
                        {
                            // Due to floating point rounding we may end up with exactly the maximum number of rows, so adjust to compensate
                            --position.Y;
                        }

                        offset += _BytesPerRow * (long)position.Y;
                    }

                    break;

                case SelectionArea.Data:
                    {
                        var pix_CharsBetweenSections = _CharsBetweenSections * _TextMeasure.Width;

                        // Clamp the X coordinate to within the data region
                        position.X = position.X.Clamp(_AddressRect.Left + pix_CharsBetweenSections, _DataRect.Left - pix_CharsBetweenSections);

                        // Normalize with respect to the data region
                        position.X -= _AddressRect.Left + pix_CharsBetweenSections;

                        // Convert the X coordinate to the column number
                        position.X /= (CalculateDataColumnCharWidth() + _CharsBetweenDataColumns) * _TextMeasure.Width;

                        if (position.X >= Columns)
                        {
                            // Due to floating point rounding we may end up with exactly the maximum number of columns, so adjust to compensate
                            --position.X;
                        }

                        // Clamp the Y coordinate to within the data region
                        position.Y = position.Y.Clamp(_DataRect.Top, _DataRect.Bottom);

                        // Convert the Y coordinate to the row number
                        position.Y /= _TextMeasure.Height;

                        if (position.Y >= MaxVisibleRows)
                        {
                            // Due to floating point rounding we may end up with exactly the maximum number of rows, so adjust to compensate
                            --position.Y;
                        }

                        offset += ((long)position.Y * Columns + (long)position.X) * _BytesPerColumn;
                    }

                    break;

                case SelectionArea.Text:
                    {
                        var pix_CharsBetweenSections = _CharsBetweenSections * _TextMeasure.Width;

                        // Clamp the X coordinate to within the text region
                        position.X = position.X.Clamp(_DataRect.Left + pix_CharsBetweenSections, _TextRect.Left - pix_CharsBetweenSections);

                        // Normalize with respect to the text region
                        position.X -= _DataRect.Left + pix_CharsBetweenSections;

                        // Convert the X coordinate to the column number
                        position.X /= CalculateTextColumnCharWidth() * _TextMeasure.Width;

                        if (position.X >= Columns)
                        {
                            // Due to floating point rounding we may end up with exactly the maximum number of columns, so
                            // adjust to compensate
                            --position.X;
                        }

                        // Clamp the Y coordinate to within the text region
                        position.Y = position.Y.Clamp(_TextRect.Top, _TextRect.Bottom);

                        // Convert the Y coordinate to the row number
                        position.Y /= _TextMeasure.Height;

                        if (position.Y >= MaxVisibleRows)
                        {
                            // Due to floating point rounding we may end up with exactly the maximum number of rows, so adjust to compensate
                            --position.Y;
                        }

                        offset += ((long)position.Y * Columns + (long)position.X) * _BytesPerColumn;
                    }

                    break;

                default:
                    {
                        throw new InvalidOperationException($"Invalid highlight state ${_HighlightState}");
                    }
            }

            return offset;
        }

        private Point ConvertOffsetToPosition(long offset, SelectionArea relativeTo)
        {
            Point position = default;

            switch (relativeTo)
            {
                case SelectionArea.Data:
                    {
                        position.X = _AddressRect.Left + _CharsBetweenSections * _TextMeasure.Width;
                        position.Y = _AddressRect.Top;

                        // Normalize requested offset to a zero based column
                        long normalizedColumn = (offset - Offset) / _BytesPerColumn;

                        position.X += (normalizedColumn % Columns + Columns) % Columns * (CalculateDataColumnCharWidth() + _CharsBetweenDataColumns) * _TextMeasure.Width;

                        if (normalizedColumn < 0)
                        {
                            // Negative normalized offset means the Y position is above the current offset. Because division
                            // rounds toward zero we need to compensate here.
                            position.Y += ((normalizedColumn + 1) / Columns - 1) * _TextMeasure.Height;
                        }
                        else
                        {
                            position.Y += normalizedColumn / Columns * _TextMeasure.Height;
                        }
                    }

                    break;

                case SelectionArea.Text:
                    {
                        position.X = _DataRect.Left + _CharsBetweenSections * _TextMeasure.Width;
                        position.Y = _DataRect.Top;

                        // Normalize requested offset to a zero based column
                        long normalizedColumn = (offset - Offset) / _BytesPerColumn;

                        position.X += (normalizedColumn % Columns + Columns) % Columns * CalculateTextColumnCharWidth() * _TextMeasure.Width;

                        if (normalizedColumn < 0)
                        {
                            // Negative normalized offset means the Y position is above the current offset. Because division
                            // rounds toward zero we need to compensate here.
                            position.Y += ((normalizedColumn + 1) / Columns - 1) * _TextMeasure.Height;
                        }
                        else
                        {
                            position.Y += normalizedColumn / Columns * _TextMeasure.Height;
                        }
                    }

                    break;

                default:
                    {
                        throw new ArgumentException($"Invalid relative area {relativeTo}", nameof(relativeTo));
                    }
            }

            return position;
        }

        private bool IsOffsetVisible(long offset)
        {
            long maxBytesDisplayed = _BytesPerRow * MaxVisibleRows;

            return Offset <= offset && Offset + maxBytesDisplayed >= offset;
        }

        /// <summary>
        /// Show the context menu programatical.
        /// Invoked if Application key or SCHIFT+F10 is pressed.
        /// </summary>
        private void ShowContextMenu()
        {
            // Get offset for context menu
            var lastVisibleOffset = Offset + (_BytesPerRow * MaxVisibleRows) - 1;
            var offset = Math.Max(Math.Max(SelectionStart, SelectionEnd), Offset);
            var palcementOffset = Math.Min(offset, lastVisibleOffset);

            // Show menu
            if (ShowData)
            {
                _Canvas.ContextFlyout.ShowAt(_Canvas, new FlyoutShowOptions
                {
                    Position = ConvertOffsetToPosition(palcementOffset, SelectionArea.Data),
                });
            }
            else if (ShowText)
            {
                _Canvas.ContextFlyout.ShowAt(_Canvas, new FlyoutShowOptions
                {
                    Position = ConvertOffsetToPosition(palcementOffset, SelectionArea.Text),
                });
            }
            else
            {
                _Canvas.ContextFlyout.ShowAt(_Canvas, new FlyoutShowOptions
                {
                    Position = new Point(0, 0),
                });
            }
        }

        /// <summary>
        /// Initializes static members of the <see cref="HexBox"/> class.
        /// </summary>
        public HexBox()
        {
            DefaultStyleKey = typeof(HexBox);
        }
    }

}
