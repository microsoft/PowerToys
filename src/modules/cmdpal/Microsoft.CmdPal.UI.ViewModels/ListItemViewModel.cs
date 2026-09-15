// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ListItemViewModel : CommandItemViewModel
{
    private const int MaxVisibleTags = 3;

    public new ExtensionObject<IListItem> Model { get; }

    public List<TagViewModel>? Tags { get; set; }

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public bool HasTags => (Tags?.Count ?? 0) > 0;

    public List<TagViewModel>? VisibleTags { get; private set; }

    private TagViewModel? _overflowTag;

    public string TextToSuggest { get; private set; } = string.Empty;

    public string Section { get; private set; } = string.Empty;

    private CommandViewModel? _sectionCommand;

    public CommandViewModel? SectionCommand => _sectionCommand;

    public string SectionCommandName => _sectionCommand?.Name ?? string.Empty;

    public bool HasSectionCommand => !string.IsNullOrEmpty(SectionCommandName);

    public string SectionCommandAccessibleName => HasSectionCommand ? $"{Section}, {SectionCommandName}" : Section;

    public bool IsSectionCommandTarget => Type == ListItemType.SectionHeader && HasSectionCommand;

    public ListItemType Type { get; private set; }

    public bool IsInteractive => Type == ListItemType.Item;

    public bool IsKeyboardNavigable => IsInteractive || IsSectionCommandTarget;

    public bool IsSectionCommandSelected { get; private set; }

    public DetailsViewModel? Details { get; private set; }

    [MemberNotNullWhen(true, nameof(Details))]
    public bool HasDetails => Details is not null;

    public string AccessibleName { get; private set; } = string.Empty;

    public bool ShowTitle { get; private set; }

    public bool ShowSubtitle { get; private set; }

    public bool LayoutShowsTitle
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                UpdateProperty(nameof(LayoutShowsTitle));
                UpdateShowsTitle();
            }
        }
    }

    public bool LayoutShowsSubtitle
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                UpdateProperty(nameof(LayoutShowsSubtitle));
                UpdateShowsSubtitle();
            }
        }
    }

    public ListItemViewModel(IListItem model, WeakReference<IPageContext> context, IContextMenuFactory contextMenuFactory)
        : base(new(model), context, contextMenuFactory)
    {
        Model = new ExtensionObject<IListItem>(model);
    }

    public override void InitializeProperties()
    {
        if (IsInitialized)
        {
            return;
        }

        // This sets IsInitialized = true
        base.InitializeProperties();

        var li = Model.Unsafe;
        if (li is null)
        {
            return; // throw?
        }

        UpdateTags(li.Tags);
        Section = li.Section ?? string.Empty;
        Type = EvaluateType();
        UpdateProperty(nameof(Section), nameof(SectionCommandAccessibleName), nameof(Type), nameof(IsInteractive), nameof(IsSectionCommandTarget), nameof(IsKeyboardNavigable));

        UpdateAccessibleName();
    }

    private ListItemType EvaluateType()
    {
        return Command.IsSet
            ? ListItemType.Item
            : string.IsNullOrEmpty(Section) ? ListItemType.Separator : ListItemType.SectionHeader;
    }

    public override void SlowInitializeProperties()
    {
        base.SlowInitializeProperties();
        var model = Model.Unsafe;
        if (model is null)
        {
            return;
        }

        var extensionDetails = model.Details;
        if (extensionDetails is not null)
        {
            Details = new(extensionDetails, PageContext);
            Details.InitializeProperties();
            UpdateProperty(nameof(Details), nameof(HasDetails));
        }

        AddShowDetailsCommands();

        TextToSuggest = model.TextToSuggest;
        UpdateProperty(nameof(TextToSuggest));
    }

    protected override void UpdateExtendedAttributes(IDictionary<string, object?>? properties)
    {
        base.UpdateExtendedAttributes(properties);
        UpdateSectionCommand(properties);
    }

    protected override void FetchProperty(string propertyName)
    {
        base.FetchProperty(propertyName);

        var model = this.Model.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(model.Tags):
                UpdateTags(model.Tags);
                break;
            case nameof(model.TextToSuggest):
                TextToSuggest = model.TextToSuggest ?? string.Empty;
                UpdateProperty(nameof(TextToSuggest));
                break;
            case nameof(model.Section):
                Section = model.Section ?? string.Empty;
                Type = EvaluateType();
                UpdateProperty(nameof(Section), nameof(SectionCommandAccessibleName), nameof(Type), nameof(IsInteractive), nameof(IsSectionCommandTarget), nameof(IsKeyboardNavigable));
                if (!IsSectionCommandTarget)
                {
                    SetSectionCommandSelected(false);
                }

                break;
            case nameof(model.Command):
                Type = EvaluateType();
                UpdateProperty(nameof(Type), nameof(IsInteractive), nameof(IsSectionCommandTarget), nameof(IsKeyboardNavigable));
                if (!IsSectionCommandTarget)
                {
                    SetSectionCommandSelected(false);
                }

                break;
            case nameof(SectionCommand):
                UpdateSectionCommand(GetExtendedAttributes());
                break;
            case nameof(Details):
                var existingReference = Details;
                var extensionDetails = model.Details;
                Details = extensionDetails is not null ? new(extensionDetails, PageContext) : null;
                Details?.InitializeProperties();
                UpdateProperty(nameof(Details), nameof(HasDetails));
                UpdateShowDetailsCommand();
                existingReference?.SafeCleanup();
                break;
            case nameof(model.MoreCommands):
                AddShowDetailsCommands();
                break;
            case nameof(model.Title):
                UpdateProperty(nameof(Title));
                UpdateShowsTitle();
                UpdateAccessibleName();
                break;
            case nameof(model.Subtitle):
                UpdateProperty(nameof(Subtitle));
                UpdateShowsSubtitle();
                UpdateAccessibleName();
                break;
            default:
                UpdateProperty(propertyName);
                break;
        }
    }

    // TODO: Do we want filters to match descriptions and other properties? Tags, etc... Yes?
    // TODO: Do we want to save off the score here so we can sort by it in our ListViewModel?
    public override string ToString() =>
        Type == ListItemType.SectionHeader ? SectionCommandAccessibleName : $"{Name} ListItemViewModel";

    public override bool Equals(object? obj) => obj is ListItemViewModel vm && vm.Model.Equals(this.Model);

    public override int GetHashCode() => Model.GetHashCode();

    public void SetSectionCommandSelected(bool value)
    {
        value &= IsSectionCommandTarget;
        if (IsSectionCommandSelected == value)
        {
            return;
        }

        IsSectionCommandSelected = value;
        UpdateProperty(nameof(IsSectionCommandSelected));
    }

    [RelayCommand]
    private void InvokeSectionCommand()
    {
        if (_sectionCommand?.Model.Unsafe is not null)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(_sectionCommand.Model, Model));
        }
    }

    private void UpdateSectionCommand(IDictionary<string, object?>? properties)
    {
        var command =
            properties?.TryGetValue(WellKnownExtensionAttributes.SectionCommand, out var value) == true
                ? value as ICommand
                : null;

        if (ReferenceEquals(_sectionCommand?.Model.Unsafe, command))
        {
            return;
        }

        var replacement = command is null ? null : new CommandViewModel(command, PageContext);
        replacement?.InitializeProperties();
        if (replacement is not null)
        {
            replacement.PropertyChanged += SectionCommand_PropertyChanged;
        }

        var previous = _sectionCommand;
        _sectionCommand = replacement;
        if (previous is not null)
        {
            previous.PropertyChanged -= SectionCommand_PropertyChanged;
            previous.SafeCleanup();
        }

        UpdateProperty(
            nameof(SectionCommand),
            nameof(SectionCommandName),
            nameof(HasSectionCommand),
            nameof(SectionCommandAccessibleName),
            nameof(IsSectionCommandTarget),
            nameof(IsKeyboardNavigable));
        if (!IsSectionCommandTarget)
        {
            SetSectionCommandSelected(false);
        }
    }

    private void SectionCommand_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(CommandViewModel.Name))
        {
            UpdateProperty(
                nameof(SectionCommandName),
                nameof(HasSectionCommand),
                nameof(SectionCommandAccessibleName),
                nameof(IsSectionCommandTarget),
                nameof(IsKeyboardNavigable));
            if (!IsSectionCommandTarget)
            {
                SetSectionCommandSelected(false);
            }
        }
    }

    private void AddShowDetailsCommands()
    {
        // If the parent page has ShowDetails = false and we have details,
        // then we should add a show details action in the context menu.
        if (HasDetails &&
            PageContext.TryGetTarget(out var pageContext) &&
            pageContext is ListViewModel listViewModel &&
            !listViewModel.ShowDetails)
        {
            var addedCommand = false;
            lock (MoreCommandsLock)
            {
                // Check if "Show Details" action already exists to prevent duplicates
                if (!UnsafeMoreCommands.Any(cmd => cmd is CommandContextItemViewModel contextItemViewModel &&
                                                  contextItemViewModel.Command.Id == ShowDetailsCommand.ShowDetailsCommandId))
                {
                    var showDetailsCommand = new ShowDetailsCommand(Details);
                    var showDetailsContextItem = new CommandContextItem(showDetailsCommand)
                    {
                        Icon = showDetailsCommand.Icon,
                    };
                    var showDetailsContextItemViewModel = new CommandContextItemViewModel(showDetailsContextItem, PageContext);
                    showDetailsContextItemViewModel.SlowInitializeProperties();
                    UnsafeMoreCommands.Add(showDetailsContextItemViewModel);
                    RefreshMoreCommandStateUnsafe();
                    addedCommand = true;
                }
            }

            if (addedCommand)
            {
                UpdateProperty(nameof(MoreCommands), nameof(AllCommands));
                UpdateProperty(nameof(SecondaryCommand), nameof(SecondaryCommandName), nameof(HasMoreCommands));
            }
        }
    }

    // This method is called when the details change to make sure we
    // have the latest details in the show details command.
    private void UpdateShowDetailsCommand()
    {
        // If the parent page has ShowDetails = false and we have details,
        // then we should add a show details action in the context menu.
        if (HasDetails &&
            PageContext.TryGetTarget(out var pageContext) &&
            pageContext is ListViewModel listViewModel &&
            !listViewModel.ShowDetails)
        {
            CommandContextItemViewModel? oldCommand = null;
            lock (MoreCommandsLock)
            {
                oldCommand = UnsafeMoreCommands
                    .OfType<CommandContextItemViewModel>()
                    .FirstOrDefault(contextItemViewModel => contextItemViewModel.Command.Id == ShowDetailsCommand.ShowDetailsCommandId);

                if (oldCommand is not null)
                {
                    UnsafeMoreCommands.Remove(oldCommand);
                }

                var showDetailsCommand = new ShowDetailsCommand(Details);
                var showDetailsContextItem = new CommandContextItem(showDetailsCommand)
                {
                    Icon = showDetailsCommand.Icon,
                };
                var showDetailsContextItemViewModel = new CommandContextItemViewModel(showDetailsContextItem, PageContext);
                showDetailsContextItemViewModel.SlowInitializeProperties();
                UnsafeMoreCommands.Add(showDetailsContextItemViewModel);
                RefreshMoreCommandStateUnsafe();
            }

            oldCommand?.SafeCleanup();

            UpdateProperty(nameof(MoreCommands), nameof(AllCommands));
            UpdateProperty(nameof(SecondaryCommand), nameof(SecondaryCommandName), nameof(HasMoreCommands));
        }
    }

    private void UpdateTags(ITag[]? newTagsFromModel)
    {
        var newTags = newTagsFromModel?.Select(t =>
        {
            var vm = new TagViewModel(t, PageContext);
            vm.InitializeProperties();
            return vm;
        })
            .ToList() ?? [];

        DoOnUiThread(
            () =>
            {
                // Tags being an ObservableCollection instead of a List lead to
                // many COM exception issues.
                Tags = [.. newTags];
                UpdateVisibleTags();

                // We're already in UI thread, so just raise the events
                OnPropertyChanged(nameof(Tags));
                OnPropertyChanged(nameof(HasTags));
                OnPropertyChanged(nameof(VisibleTags));
            });
    }

    private void UpdateVisibleTags()
    {
        var allTags = Tags;
        if (allTags is null || allTags.Count == 0)
        {
            VisibleTags = null;
        }
        else if (allTags.Count <= MaxVisibleTags)
        {
            VisibleTags = [.. allTags];
        }
        else
        {
            _overflowTag?.SafeCleanup();
            var visible = allTags.Take(MaxVisibleTags).ToList();
            var overflowCount = allTags.Count - MaxVisibleTags;
            var hiddenTagNames = allTags.Skip(MaxVisibleTags).Select(t => t.Text);
            var overflowTag = new TagViewModel(
                new Tag($"+{overflowCount}")
                {
                    ToolTip = string.Join("\n", hiddenTagNames),
                },
                PageContext);
            overflowTag.InitializeProperties();
            _overflowTag = overflowTag;
            visible.Add(overflowTag);
            VisibleTags = visible;
        }
    }

    private void UpdateShowsTitle()
    {
        var oldShowTitle = ShowTitle;
        ShowTitle = LayoutShowsTitle;
        if (oldShowTitle != ShowTitle)
        {
            UpdateProperty(nameof(ShowTitle));
        }
    }

    private void UpdateShowsSubtitle()
    {
        var oldShowSubtitle = ShowSubtitle;
        ShowSubtitle = LayoutShowsSubtitle && !string.IsNullOrWhiteSpace(Subtitle);
        if (oldShowSubtitle != ShowSubtitle)
        {
            UpdateProperty(nameof(ShowSubtitle));
        }
    }

    protected override void UnsafeCleanup()
    {
        CleanupInitializationState();

        base.UnsafeCleanup();

        // Tags don't have event handlers or anything to cleanup
        Tags?.ForEach(t => t.SafeCleanup());
        _overflowTag?.SafeCleanup();
        Details?.SafeCleanup();
        if (_sectionCommand is not null)
        {
            _sectionCommand.PropertyChanged -= SectionCommand_PropertyChanged;
            _sectionCommand.SafeCleanup();
            _sectionCommand = null;
        }

        var model = Model.Unsafe;
        if (model is not null)
        {
            // We don't need to revoke the PropChanged event handler here,
            // because we are just overriding CommandItem's FetchProperty and
            // piggy-backing off their PropChanged
        }
    }

    protected void UpdateAccessibleName()
    {
        AccessibleName = Title + ", " + Subtitle;
        UpdateProperty(nameof(AccessibleName));
    }
}
