// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Dispatching;

namespace Microsoft.CmdPal.Core.ViewModels;

public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private static readonly PropertyChangedEventArgs CountChanged = new PropertyChangedEventArgs("Count");
    private static readonly PropertyChangedEventArgs IndexerChanged = new PropertyChangedEventArgs("Item[]");
    private static readonly NotifyCollectionChangedEventArgs ResetChange = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);

    private readonly DispatcherQueue _dispatcher;

    private int _rangeOperationCount;
    private bool _collectionChangedDuringRangeOperation;
    private ReadOnlyObservableCollection<T>? _readOnlyAccessor;

    public BulkObservableCollection()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    public void AddRange(IEnumerable<T>? items)
    {
        if (items == null || !items.Any<T>())
        {
            return;
        }

        if (_dispatcher.HasThreadAccess)
        {
            try
            {
                BeginBulkOperation();
                _collectionChangedDuringRangeOperation = true;
                foreach (T obj in items)
                {
                    Items.Add(obj);
                }
            }
            finally
            {
                EndBulkOperation();
            }
        }
        else
        {
            _dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () => AddRange(items));
        }
    }

    public void BeginBulkOperation()
    {
        ++_rangeOperationCount;
        _collectionChangedDuringRangeOperation = false;
    }

    public void EndBulkOperation()
    {
        if (_rangeOperationCount <= 0 || --_rangeOperationCount != 0 || !_collectionChangedDuringRangeOperation)
        {
            return;
        }

        OnPropertyChanged(CountChanged);
        OnPropertyChanged(IndexerChanged);
        OnCollectionChanged(ResetChange);
    }

    public ReadOnlyObservableCollection<T> AsReadOnly()
    {
        if (_readOnlyAccessor == null)
        {
            _readOnlyAccessor = new ReadOnlyObservableCollection<T>((ObservableCollection<T>)this);
        }

        return _readOnlyAccessor;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (_rangeOperationCount == 0)
        {
            base.OnPropertyChanged(e);
        }
        else
        {
            _collectionChangedDuringRangeOperation = true;
        }
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_rangeOperationCount == 0)
        {
            base.OnCollectionChanged(e);
        }
        else
        {
            _collectionChangedDuringRangeOperation = true;
        }
    }

    protected override void SetItem(int index, T item)
    {
        if (_dispatcher.HasThreadAccess)
        {
            base.SetItem(index, item);
        }
        else
        {
            _dispatcher.TryEnqueue(DispatcherQueuePriority.High, () => base.SetItem(index, item));
        }
    }

    protected override void InsertItem(int index, T item)
    {
        if (_dispatcher.HasThreadAccess)
        {
            if (_rangeOperationCount == 0)
            {
                base.InsertItem(index, item);
            }
            else
            {
                Items.Insert(index, item);
                _collectionChangedDuringRangeOperation = true;
            }
        }
        else
        {
            _dispatcher.TryEnqueue(DispatcherQueuePriority.High, () => base.InsertItem(index, item));
        }
    }

    protected override void MoveItem(int oldIndex, int newIndex)
    {
        if (_dispatcher.HasThreadAccess)
        {
            base.MoveItem(oldIndex, newIndex);
        }
        else
        {
            _dispatcher.TryEnqueue(DispatcherQueuePriority.High, () => base.MoveItem(oldIndex, newIndex));
        }
    }

    protected override void RemoveItem(int index)
    {
        if (_dispatcher.HasThreadAccess)
        {
            base.RemoveItem(index);
        }
        else
        {
            _dispatcher.TryEnqueue(DispatcherQueuePriority.High, () => base.RemoveItem(index));
        }
    }

    protected override void ClearItems()
    {
        if (_dispatcher.HasThreadAccess)
        {
            if (Count <= 0)
            {
                return;
            }

            base.ClearItems();
        }
        else
        {
            _dispatcher.TryEnqueue(DispatcherQueuePriority.High, () => base.ClearItems());
        }
    }
}
