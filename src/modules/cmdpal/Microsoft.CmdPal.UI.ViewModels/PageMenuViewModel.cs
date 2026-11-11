// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class PageMenuViewModel : ExtensionObjectViewModel, IContextMenuContext
{
    private readonly ExtensionObject<IPage> _pageModel;
    private readonly IContextMenuFactory _contextMenuFactory;
    private readonly Lock _menuLock = new();
    private volatile IContextItemViewModel[] _commands = [];
    private volatile IContextItemViewModel[] _visibleCommands = [];
    private IContextItem[]? _items;
    private int _revision;
    private bool _isCleanedUp;

    public IReadOnlyList<IContextItemViewModel> AllCommands => _visibleCommands;

    public bool CanOpenContextMenu => _visibleCommands.Length > 0;

    internal PageMenuViewModel(ExtensionObject<IPage> pageModel, WeakReference<IPageContext> pageContext, IContextMenuFactory contextMenuFactory)
        : base(pageContext)
    {
        _pageModel = pageModel;
        _contextMenuFactory = contextMenuFactory;
    }

    public override void InitializeProperties()
    {
        int revision;
        lock (_menuLock)
        {
            if (_isCleanedUp)
            {
                return;
            }

            revision = ++_revision;
        }

        IContextItem[] items = [];
        IContextItemViewModel[] commands = [];
        Exception? extensionError = null;
        Exception? hostError = null;
        try
        {
            items = ReadCommands();
        }
        catch (Exception ex)
        {
            extensionError = ex;
        }

        lock (_menuLock)
        {
            if (_isCleanedUp || revision != _revision ||
                (extensionError is null && _items is not null && _items.SequenceEqual(items, ReferenceEqualityComparer.Instance)))
            {
                return;
            }
        }

        try
        {
            commands = [.. DefaultContextMenuFactory.Instance.UnsafeBuildAndInitMoreCommands(items, PageContext)];
        }
        catch (Exception ex)
        {
            extensionError = extensionError is null ? ex : new AggregateException(extensionError, ex);
        }

        try
        {
            List<IContextItem> contextItems = extensionError is null ? [.. items] : [];
            var extensionItemCount = contextItems.Count;
            if (PageContext.TryGetTarget(out var context) && context is PageViewModel page)
            {
                _contextMenuFactory.AddMoreCommandsToPage(page, contextItems);
            }

            var hostCommands = DefaultContextMenuFactory.Instance.UnsafeBuildAndInitMoreCommands([.. contextItems.Skip(extensionItemCount)], PageContext);
            commands = [.. commands, .. hostCommands];
        }
        catch (Exception ex)
        {
            hostError = ex;
        }

        IContextItemViewModel[] removed;
        var published = false;
        lock (_menuLock)
        {
            if (_isCleanedUp || revision != _revision)
            {
                removed = commands;
            }
            else
            {
                removed = _commands;
                foreach (var command in removed.OfType<CommandContextItemViewModel>())
                {
                    command.PropertyChangedBackground -= Command_PropertyChanged;
                }

                _commands = commands;
                _visibleCommands = GetVisibleCommands(commands);
                _items = extensionError is null && hostError is null ? items : null;
                foreach (var command in commands.OfType<CommandContextItemViewModel>())
                {
                    command.PropertyChangedBackground += Command_PropertyChanged;
                }

                published = true;
            }
        }

        DefaultContextMenuFactory.CleanupCommands(removed);
        if (published)
        {
            if (extensionError is not null)
            {
                ShowException(extensionError);
            }

            if (hostError is not null)
            {
                CoreLogger.LogError("Failed to build host commands for the page menu.", hostError);

                // An empty hint attributes the error to the application instead of the page's extension.
                ShowException(hostError, extensionHint: string.Empty);
            }

            UpdateProperty(nameof(AllCommands), nameof(CanOpenContextMenu));
        }
    }

    private static IContextItemViewModel[] GetVisibleCommands(IContextItemViewModel[] commands)
    {
        List<IContextItemViewModel> visible = [];
        SeparatorViewModel? separator = null;
        foreach (var item in commands)
        {
            if (item is SeparatorViewModel candidate)
            {
                if (visible.Count > 0)
                {
                    separator = candidate;
                }
            }
            else if (item is CommandContextItemViewModel { ShouldBeVisible: true })
            {
                if (separator is not null)
                {
                    visible.Add(separator);
                    separator = null;
                }

                visible.Add(item);
            }
        }

        return [.. visible];
    }

    private IContextItem[] ReadCommands()
    {
        var properties = (_pageModel.Unsafe as IExtendedAttributesProvider)?.GetProperties();
        if (properties is null || !properties.TryGetValue(WellKnownExtensionAttributes.PageMenuCommands, out var value) || value is null)
        {
            return [];
        }

        // C#/WinRT 2.2 needs JIT-only helpers for inherited collection Count; enumerate the payload directly.
        return value switch
        {
            IEnumerable<IContextItem> items => ReadCommands(items),
            IEnumerable<object> objects => ReadCommands(objects),
            _ => throw new InvalidOperationException($"{WellKnownExtensionAttributes.PageMenuCommands} must contain a collection of context items."),
        };
    }

    private static IContextItem[] ReadCommands<T>(IEnumerable<T> values)
    {
        List<IContextItem> items = [];
        foreach (var value in values)
        {
            items.Add(value as IContextItem
                ?? throw new InvalidOperationException($"{WellKnownExtensionAttributes.PageMenuCommands} contains an invalid context item."));
        }

        return [.. items];
    }

    private void Command_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(CommandItemViewModel.Name))
        {
            return;
        }

        lock (_menuLock)
        {
            if (_isCleanedUp
                || sender is not CommandContextItemViewModel command
                || !_commands.Contains(command)
                || command.ShouldBeVisible == _visibleCommands.Contains(command))
            {
                return;
            }

            _visibleCommands = GetVisibleCommands(_commands);
        }

        UpdateProperty(nameof(AllCommands), nameof(CanOpenContextMenu));
    }

    protected override void UnsafeCleanup()
    {
        IContextItemViewModel[] removed;
        lock (_menuLock)
        {
            _isCleanedUp = true;
            removed = _commands;
            _commands = [];
            _visibleCommands = [];
            _items = null;
            foreach (var command in removed.OfType<CommandContextItemViewModel>())
            {
                command.PropertyChangedBackground -= Command_PropertyChanged;
            }
        }

        DefaultContextMenuFactory.CleanupCommands(removed);
    }
}
