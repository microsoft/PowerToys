// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.CommandPalette.Extensions;
using System.ComponentModel;
using Microsoft.UI.Dispatching;

namespace DeveloperCommandPalette;

public sealed class ListItemViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherQueue DispatcherQueue;
    internal IListItem ListItem { get; init; }
    internal string Title { get; private set; }
    internal string Subtitle { get; private set; }
    internal string Icon { get; private set; }

    internal Lazy<DetailsViewModel?> _Details;
    internal DetailsViewModel? Details => _Details.Value;
    internal IFallbackHandler? FallbackHandler => this.ListItem.FallbackHandler;

    public event PropertyChangedEventHandler? PropertyChanged;

    internal ICommand DefaultAction => ListItem.Command;
    internal bool CanInvoke => DefaultAction != null && DefaultAction is IInvokableCommand or IPage;
    internal IconElement IcoElement => Microsoft.Terminal.UI.IconPathConverter.IconMUX(Icon);

    private IEnumerable<ICommandContextItem> contextActions => ListItem.MoreCommands == null ? [] : ListItem.MoreCommands.Where(i => i is ICommandContextItem).Select(i=> (ICommandContextItem)i);
    internal bool HasMoreCommands => contextActions.Any();

    internal TagViewModel[] Tags = [];
    internal bool HasTags => Tags.Length > 0;

    internal IList<ContextItemViewModel> ContextActions
    {
        get
        {
            var l = contextActions.Select(a => new ContextItemViewModel(a)).ToList();
            l.Insert(0, new(DefaultAction));
            return l;
        }
    }

    public ListItemViewModel(IListItem model)
    {
        model.PropChanged += ListItem_PropertyChanged;
        this.ListItem = model;
        this.Title = model.Title;
        this.Subtitle = model.Subtitle;
        this.Icon = model.Command.Icon.Icon;
        if (model.Tags != null)
        {
            this.Tags = model.Tags.Select(t => new TagViewModel(t)).ToArray();
        }

        this._Details = new(() => model.Details != null ? new(this.ListItem.Details) : null);

        this.DispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    private void ListItem_PropertyChanged(object sender, Microsoft.Windows.CommandPalette.Extensions.PropChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case "Name":
            case nameof(Title):
                {
                    this.Title = ListItem.Title;
                }
                break;
            case nameof(Subtitle):
                {
                    this.Subtitle = ListItem.Subtitle;
                }
                break;
            case "MoreCommands":
                {
                    BubbleXamlPropertyChanged(nameof(HasMoreCommands));
                    BubbleXamlPropertyChanged(nameof(ContextActions));
                }
                break;
            case nameof(Icon):
                {
                    this.Icon = ListItem.Command.Icon.Icon;
                    BubbleXamlPropertyChanged(nameof(IcoElement));
                }
                break;
        }
        BubbleXamlPropertyChanged(args.PropertyName);

    }

    private void BubbleXamlPropertyChanged(string propertyName)
    {
        if (this.DispatcherQueue == null)
        {
            // this is highly unusual
            return;
        }
        this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
        {
            this.PropertyChanged?.Invoke(this, new(propertyName));
        });
    }

    public void Dispose()
    {
        this.ListItem.PropChanged -= ListItem_PropertyChanged;

    }
}
