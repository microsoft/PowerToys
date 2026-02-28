// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;

using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Peek.Common.Helpers;
using Peek.FilePreviewer.Previewers;
using Peek.FilePreviewer.Previewers.SQLitePreviewer;
using Peek.FilePreviewer.Previewers.SQLitePreviewer.Models;

namespace Peek.FilePreviewer.Controls
{
    public sealed partial class SQLiteControl : UserControl
    {
        public static readonly DependencyProperty TablesProperty = DependencyProperty.Register(
            nameof(Tables),
            typeof(ObservableCollection<SQLiteTableInfo>),
            typeof(SQLitePreviewer),
            new PropertyMetadata(null, OnTablesPropertyChanged));

        public static readonly DependencyProperty LoadingStateProperty = DependencyProperty.Register(
            nameof(LoadingState),
            typeof(PreviewState),
            typeof(SQLitePreviewer),
            new PropertyMetadata(PreviewState.Uninitialized));

        public static readonly DependencyProperty TableCountProperty = DependencyProperty.Register(
            nameof(TableCount),
            typeof(string),
            typeof(SQLitePreviewer),
            new PropertyMetadata(null));

        private double _lastColumnAutoWidth = double.NaN;

        public ObservableCollection<SQLiteTableInfo>? Tables
        {
            get => (ObservableCollection<SQLiteTableInfo>?)GetValue(TablesProperty);
            set => SetValue(TablesProperty, value);
        }

        public PreviewState? LoadingState
        {
            get => (PreviewState)GetValue(LoadingStateProperty);
            set => SetValue(LoadingStateProperty, value);
        }

        public string? TableCount
        {
            get => (string?)GetValue(TableCountProperty);
            set => SetValue(TableCountProperty, value);
        }

        public SQLiteControl()
        {
            this.InitializeComponent();
        }

        private static void OnTablesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (SQLiteControl)d;

            if (e.OldValue is ObservableCollection<SQLiteTableInfo> oldCollection)
            {
                oldCollection.CollectionChanged -= control.OnTablesCollectionChanged;
            }

            control.TableTreeView.RootNodes.Clear();
            control.ClearDataView();

            if (e.NewValue is ObservableCollection<SQLiteTableInfo> newCollection)
            {
                newCollection.CollectionChanged += control.OnTablesCollectionChanged;
                foreach (var table in newCollection)
                {
                    control.AddTableNode(table);
                }
            }
        }

        private void OnTablesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (SQLiteTableInfo table in e.NewItems)
                {
                    AddTableNode(table);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                TableTreeView.RootNodes.Clear();
                ClearDataView();
            }
        }

        private void AddTableNode(SQLiteTableInfo table)
        {
            var tableNode = new TreeViewNode { Content = table };
            foreach (var col in table.Columns)
            {
                tableNode.Children.Add(new TreeViewNode { Content = col.DisplayText });
            }

            TableTreeView.RootNodes.Add(tableNode);
        }

        private void TableTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            if (args.InvokedItem is TreeViewNode { Content: SQLiteTableInfo table })
            {
                ShowTableData(table);
            }
        }

        private void ShowTableData(SQLiteTableInfo table)
        {
            _lastColumnAutoWidth = double.NaN;
            TableDataGrid.Columns.Clear();
            foreach (var col in table.Columns)
            {
                TableDataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = col.Name,
                    Binding = new Binding { Path = new PropertyPath($"[{col.Name}]") },
                    IsReadOnly = true,
                });
            }

            TableDataGrid.ItemsSource = table.Rows;

            // After columns and rows are set, defer measurement until layout has completed
            // so ActualWidth values are valid when we decide whether to stretch the last column.
            TableDataGrid.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, AdjustLastColumnWidth);

            RecordCountText.Text = string.Format(
                CultureInfo.CurrentCulture,
                ResourceLoaderInstance.ResourceLoader.GetString("SQLite_Row_Count"),
                table.RowCount);

            RecordCountHeader.Visibility = Visibility.Visible;
            TableDataGrid.Visibility = Visibility.Visible;
            NoSelectionText.Visibility = Visibility.Collapsed;
        }

        private void TableDataGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustLastColumnWidth();
        }

        private void AdjustLastColumnWidth()
        {
            if (TableDataGrid.Columns.Count == 0 || TableDataGrid.ActualWidth <= 0)
            {
                return;
            }

            var lastCol = TableDataGrid.Columns[TableDataGrid.Columns.Count - 1];

            // Capture the last column's natural auto-width the first time it is measured.
            // Once the column is Star-stretched we keep using the stored value so that
            // window resizes can correctly revert to Auto when the grid becomes too narrow.
            if (!lastCol.Width.IsStar && lastCol.ActualWidth > 0)
            {
                _lastColumnAutoWidth = lastCol.ActualWidth;
            }

            if (double.IsNaN(_lastColumnAutoWidth) || _lastColumnAutoWidth <= 0)
            {
                return;
            }

            double otherColumnsWidth = 0;
            for (int i = 0; i < TableDataGrid.Columns.Count - 1; i++)
            {
                otherColumnsWidth += TableDataGrid.Columns[i].ActualWidth;
            }

            if (otherColumnsWidth + _lastColumnAutoWidth < TableDataGrid.ActualWidth)
            {
                lastCol.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            }
            else
            {
                lastCol.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }
        }

        private void ClearDataView()
        {
            _lastColumnAutoWidth = double.NaN;
            TableDataGrid.Columns.Clear();
            TableDataGrid.ItemsSource = null;
            RecordCountHeader.Visibility = Visibility.Collapsed;
            TableDataGrid.Visibility = Visibility.Collapsed;
            NoSelectionText.Visibility = Visibility.Visible;
        }
    }
}
