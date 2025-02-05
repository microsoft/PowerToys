// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class CommandItemViewModel : ExtensionObjectViewModel
{
    private readonly ExtensionObject<ICommandItem> _commandItemModel = new(null);
    private CommandContextItemViewModel? _defaultCommandContextItem;

    // These are properties that are "observable" from the extension object
    // itself, in the sense that they get raised by PropChanged events from the
    // extension. However, we don't want to actually make them
    // [ObservableProperty]s, because PropChanged comes in off the UI thread,
    // and ObservableProperty is not smart enough to raise the PropertyChanged
    // on the UI thread.
    public string Name { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Subtitle { get; private set; } = string.Empty;

    public IconInfoViewModel Icon { get; private set; }// = new(string.Empty);

    public ExtensionObject<ICommand> Command { get; private set; } = new(null);

    public List<CommandContextItemViewModel> MoreCommands { get; private set; } = [];

    public bool HasMoreCommands => MoreCommands.Count > 0;

    public string SecondaryCommandName => SecondaryCommand?.Name ?? string.Empty;

    public CommandItemViewModel? SecondaryCommand => HasMoreCommands ? MoreCommands[0] : null;

    public bool ShouldBeVisible => !string.IsNullOrEmpty(Name);

    public List<CommandContextItemViewModel> AllCommands
    {
        get
        {
            List<CommandContextItemViewModel> l = _defaultCommandContextItem == null ?
                new() :
                [_defaultCommandContextItem];

            l.AddRange(MoreCommands);
            return l;
        }
    }

    public CommandItemViewModel(ExtensionObject<ICommandItem> item, IPageContext errorContext)
        : base(errorContext)
    {
        _commandItemModel = item;
        Icon = new(null);
    }

    //// Called from ListViewModel on background thread started in ListPage.xaml.cs
    public override void InitializeProperties()
    {
        var model = _commandItemModel.Unsafe;
        if (model == null)
        {
            return;
        }

        Command = new(model.Command);

        // The way we're using this, this call to initialize Name is
        // particularly unsafe. For top-level commands, we wrap the
        // _CommandItem_ in a TopLevelCommandWrapper. But the secret problem
        // is: if the extension crashes, then the next time the MainPage
        // fetches items, we'll grab the TopLevelCommandWrapper, and try to get
        // the .Name out of its Command. But its .Command has died, so we
        // explode here.
        // (Icon probably has the same issue)
        // When we have proper stubs for TLC's, this probably won't be an issue anymore.
        Name = model.Command?.Name ?? string.Empty;
        Title = model.Title;
        Subtitle = model.Subtitle;

        var listIcon = model.Icon;
        var iconInfo = listIcon ?? Command.Unsafe!.Icon;
        Icon = new(iconInfo);
        Icon.InitializeProperties();

        var more = model.MoreCommands;
        if (more != null)
        {
            MoreCommands = more
                .Where(contextItem => contextItem is ICommandContextItem)
                .Select(contextItem => (contextItem as ICommandContextItem)!)
                .Select(contextItem => new CommandContextItemViewModel(contextItem, PageContext))
                .ToList();
        }

        // Here, we're already theoretically in the async context, so we can
        // use Initialize straight up
        MoreCommands.ForEach(contextItem =>
        {
            contextItem.InitializeProperties();
        });

        _defaultCommandContextItem = new(new CommandContextItem(model.Command!), PageContext)
        {
            Name = Name,
            Title = Name,
            Subtitle = Subtitle,
            Icon = Icon,
            Command = new(model.Command),

            // TODO this probably should just be a CommandContextItemViewModel(CommandItemViewModel) ctor, or a copy ctor or whatever
        };

        model.PropChanged += Model_PropChanged;

        // _initialized = true;
    }

    public bool SafeInitializeProperties()
    {
        try
        {
            InitializeProperties();
            return true;
        }
        catch (Exception)
        {
            Command = new(null);
            Name = "Error";
            Title = "Error";
            Subtitle = "Item failed to load";
            MoreCommands = [];
            Icon = new(new IconInfo("❌")); // new("❌");
            Icon.InitializeProperties();
        }

        return false;
    }

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
    {
        try
        {
            FetchProperty(args.PropertyName);
        }
        catch (Exception ex)
        {
            PageContext.ShowException(ex, _commandItemModel?.Unsafe?.Title);
        }
    }

    protected virtual void FetchProperty(string propertyName)
    {
        var model = this._commandItemModel.Unsafe;
        if (model == null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(Command):
                this.Command = new(model.Command);
                Name = model.Command?.Name ?? string.Empty;
                UpdateProperty(nameof(Name));

                break;
            case nameof(Name):
                this.Name = model.Command?.Name ?? string.Empty;
                break;
            case nameof(Title):
                this.Title = model.Title;
                break;
            case nameof(Subtitle):
                this.Subtitle = model.Subtitle;
                break;
            case nameof(Icon):
                var listIcon = model.Icon;
                var iconInfo = listIcon ?? Command.Unsafe!.Icon;
                Icon = new(iconInfo);
                Icon.InitializeProperties();
                break;

                // TODO GH #360 - make MoreCommands observable
                // which needs to also raise HasMoreCommands
        }

        UpdateProperty(propertyName);
    }
}
