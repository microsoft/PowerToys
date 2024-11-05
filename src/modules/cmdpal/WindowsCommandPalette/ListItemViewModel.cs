// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Extensions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using WindowsCommandPalette.Models;
using WindowsCommandPalette.Views;

namespace WindowsCommandPalette;

public sealed class ListItemViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;

    internal ExtensionObject<IListItem> ListItem { get; init; }

    internal string Title { get; private set; }

    internal string Subtitle { get; private set; }

    internal string Icon { get; private set; }

    private readonly Lazy<DetailsViewModel?> _details;

    internal DetailsViewModel? Details => _details.Value;

    public event PropertyChangedEventHandler? PropertyChanged;

    internal ICommand? DefaultAction
    {
        get
        {
            try
            {
                return ListItem.Unsafe.Command;
            }
            catch (COMException)
            {
                return null;
            }
        }
    }

    internal bool CanInvoke => DefaultAction != null && DefaultAction is IInvokableCommand or IPage;

    internal IconElement IcoElement => Microsoft.Terminal.UI.IconPathConverter.IconMUX(Icon);

    private IEnumerable<ICommandContextItem> AllCommands
    {
        get
        {
            try
            {
                var item = ListItem.Unsafe;
                return item.MoreCommands == null ?
                    [] :
                    item.MoreCommands.Where(i => i is ICommandContextItem).Select(i => (ICommandContextItem)i);
            }
            catch (COMException)
            {
                /* log something */
                return [];
            }
        }
    }

    internal bool HasMoreCommands => AllCommands.Any();

    public TagViewModel[] Tags { get; set; } = [];

    internal bool HasTags => Tags.Length > 0;

    internal IList<ContextItemViewModel> ContextActions
    {
        get
        {
            try
            {
                var l = AllCommands.Select(a => new ContextItemViewModel(a)).ToList();
                var def = DefaultAction;

                if (def != null)
                {
                    l.Insert(0, new(def));
                }

                return l;
            }
            catch (COMException)
            {
                /* log something */
                return [];
            }
        }
    }

    public ListItemViewModel(IListItem model)
    {
        model.PropChanged += ListItem_PropertyChanged;
        this.ListItem = new(model);
        this.Title = model.Title;
        this.Subtitle = model.Subtitle;
        this.Icon = model.Icon.Icon;

        if (model.Tags != null)
        {
            this.Tags = model.Tags.Select(t => new TagViewModel(t)).ToArray();
        }

        this._details = new(() =>
        {
            try
            {
                var item = this.ListItem.Unsafe;
                return item.Details != null ? new(item.Details) : null;
            }
            catch (COMException)
            {
                /* log something */
                return null;
            }
        });

        this._dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    private void ListItem_PropertyChanged(object sender, Microsoft.CmdPal.Extensions.PropChangedEventArgs args)
    {
        try
        {
            var item = ListItem.Unsafe;
            switch (args.PropertyName)
            {
                case "Name":
                case nameof(Title):
                    this.Title = item.Title;
                    break;
                case nameof(Subtitle):
                    this.Subtitle = item.Subtitle;
                    break;
                case "MoreCommands":
                    BubbleXamlPropertyChanged(nameof(HasMoreCommands));
                    BubbleXamlPropertyChanged(nameof(ContextActions));
                    break;
                case nameof(Icon):
                    this.Icon = item.Command.Icon.Icon;
                    BubbleXamlPropertyChanged(nameof(IcoElement));
                    break;
            }

            BubbleXamlPropertyChanged(args.PropertyName);
        }
        catch (COMException)
        {
            /* log something */
        }
    }

    private void BubbleXamlPropertyChanged(string propertyName)
    {
        if (this._dispatcherQueue == null)
        {
            // this is highly unusual
            return;
        }

        this._dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
        {
            this.PropertyChanged?.Invoke(this, new(propertyName));
        });
    }

    public void Dispose()
    {
        try
        {
            this.ListItem.Unsafe.PropChanged -= ListItem_PropertyChanged;
        }
        catch (COMException)
        {
            /* log something */
        }
    }
}
