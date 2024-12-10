// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class CommandItemViewModel : ExtensionObjectViewModel
{
    private readonly ExtensionObject<ICommandItem> _commandItemModel = new(null);

    protected TaskScheduler Scheduler { get; private set; }

    // These are properties that are "observable" from the extension object
    // itself, in the sense that they get raised by PropChanged events from the
    // extension. However, we don't want to actually make them
    // [ObservableProperty]s, because PropChanged comes in off the UI thread,
    // and ObservableProperty is not smart enough to raise the PropertyChanged
    // on the UI thread.
    public string Name { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Subtitle { get; private set; } = string.Empty;

    public IconDataType Icon { get; private set; } = new(string.Empty);

    public ExtensionObject<ICommand> Command { get; private set; } = new(null);

    public List<CommandContextItemViewModel> MoreCommands { get; private set; } = [];

    public bool HasMoreCommands => MoreCommands.Count > 0;

    public string SecondaryCommandName => HasMoreCommands ? MoreCommands[0].Name : string.Empty;

    public CommandItemViewModel? SecondaryCommand => HasMoreCommands ? MoreCommands[0] : null;

    public List<CommandContextItemViewModel> AllCommands
    {
        get
        {
            var command = _commandItemModel.Unsafe?.Command;
            var model = new CommandContextItem(command!)
            {
            };
            CommandContextItemViewModel defaultCommand = new(model, Scheduler)
            {
                Name = Name,
                Title = Name,
                Subtitle = Subtitle,
                Icon = Icon,

                // TODO this probably should just be a CommandContextItemViewModel(CommandItemViewModel) ctor, or a copy ctor or whatever
            };

            var l = new List<CommandContextItemViewModel> { defaultCommand };
            l.AddRange(MoreCommands);
            return l;
        }
    }

    public CommandItemViewModel(ExtensionObject<ICommandItem> item, TaskScheduler scheduler)
    {
        _commandItemModel = item;
        Scheduler = scheduler;
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
        Name = model.Command?.Name ?? string.Empty;
        Title = model.Title;
        Subtitle = model.Subtitle;
        Icon = model.Icon;
        MoreCommands = model.MoreCommands
            .Where(contextItem => contextItem is ICommandContextItem)
            .Select(contextItem => (contextItem as ICommandContextItem)!)
            .Select(contextItem => new CommandContextItemViewModel(contextItem, Scheduler))
            .ToList();

        // Here, we're already theoretically in the async context, so we can
        // use Initialize straight up
        MoreCommands.ForEach(contextItem =>
        {
            contextItem.InitializeProperties();
        });

        model.PropChanged += Model_PropChanged;

        // _initialized = true;
    }

    private void Model_PropChanged(object sender, PropChangedEventArgs args)
    {
        try
        {
            FetchProperty(args.PropertyName);
        }
        catch (Exception)
        {
            // TODO log? throw?
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
                this.Icon = model.Icon;
                break;

                // TODO! Icon
                // TODO! MoreCommands array, which needs to also raise HasMoreCommands
        }

        UpdateProperty(propertyName);
    }

    protected void UpdateProperty(string propertyName) => Task.Factory.StartNew(() => { OnPropertyChanged(propertyName); }, CancellationToken.None, TaskCreationOptions.None, Scheduler);
}
