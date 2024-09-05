// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using DeveloperCommandPalette;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace WindowsCommandPalette.Views;

public class SectionInfoList : ObservableCollection<ListItemViewModel>
{
    public string Title { get; }

    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    public SectionInfoList(ISection? section, IEnumerable<ListItemViewModel> items)
        : base(items)
    {
        Title = section?.Title ?? string.Empty;
        if (section != null && section is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged -= Items_CollectionChanged;
            observable.CollectionChanged += Items_CollectionChanged;
        }

        if (this._dispatcherQueue == null)
        {
            throw new InvalidOperationException("DispatcherQueue is null");
        }
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // DispatcherQueue.TryEnqueue(() => {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (var i in e.NewItems)
            {
                if (i is IListItem li)
                {
                    if (!string.IsNullOrEmpty(li.Title))
                    {
                        ListItemViewModel vm = new(li);
                        this.Add(vm);
                    }

                    // if (isDynamic)
                    // {
                    //    // Dynamic lists are in charge of their own
                    //    // filtering. They know if this thing was already
                    //    // filtered or not.
                    //    FilteredItems.Add(vm);
                    // }
                }
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            this.Clear();

            // Items.Clear();
            // if (isDynamic)
            // {
            //    FilteredItems.Clear();
            // }
        }

        // });
    }
}
