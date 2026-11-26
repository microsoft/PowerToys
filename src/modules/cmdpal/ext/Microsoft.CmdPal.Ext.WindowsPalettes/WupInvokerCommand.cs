// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.CmdPal.Ext.WindowsPalettes.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.UI.Palettes;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

internal sealed partial class WupInvokerCommand : InvokableCommand
{
    private readonly PaletteItemView _item;
    private readonly PaletteQuery? _query;
    private readonly WupAnalyticsSession? _analytics;

    internal WupInvokerCommand(
        PaletteItemView item,
        PaletteQuery? query,
        WupAnalyticsSession? analytics,
        string? commandId = null)
    {
        _item = item;
        _query = query;
        _analytics = analytics;
        Id = commandId ?? WupCommandId.Create(item.ExtensionId, item.Id, WupCommandKind.Invoke);
        Name = item.Title ?? string.Empty;
        Icon = WupIconAdapter.Create(item.Icon);
    }

    public override ICommandResult Invoke()
    {
        _analytics?.RecordSelection(_item);
        PaletteInvoker? invoker;
        try
        {
            invoker = _item.TryGetInvoker();
            if (invoker is PaletteSystemInvoker systemInvoker)
            {
                // System invokers are host actions; the factory routes the ones the bridge can perform.
                return WupUnsupportedInvokerCommand.CreateResult(_item, systemInvoker.Kind);
            }

            invoker?.Invoke(_query);
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Invoke failed for '{_item.ExtensionId}:{_item.Id}': 0x{ex.HResult:X8} {ex.Message}");
            return CommandResult.ShowToast(new ToastArgs
            {
                Message = string.Format(CultureInfo.CurrentCulture, Resources.InvocationFailed, _item.Title),
                Result = CommandResult.KeepOpen(),
            });
        }

        if (_item.Kinds.IsOfKind("InlineCommand") || _item.Kinds.IsOfKind(KnownPaletteKinds.Toggle))
        {
            return CommandResult.KeepOpen();
        }

        WupSession.End(itemSelected: true);
        return CommandResult.Dismiss();
    }
}

internal sealed partial class WupUnsupportedInvokerCommand : InvokableCommand
{
    private readonly PaletteItemView _item;
    private readonly SystemInvokerKind _kind;
    private readonly WupAnalyticsSession? _analytics;

    internal WupUnsupportedInvokerCommand(
        PaletteItemView item,
        SystemInvokerKind kind,
        WupAnalyticsSession? analytics,
        string? commandId = null)
    {
        _item = item;
        _kind = kind;
        _analytics = analytics;
        Id = commandId ?? WupCommandId.Create(item.ExtensionId, item.Id, WupCommandKind.Invoke);
        Name = item.Title ?? string.Empty;
        Icon = WupIconAdapter.Create(item.Icon);
    }

    public override ICommandResult Invoke()
    {
        _analytics?.RecordSelection(_item);
        return CreateResult(_item, _kind);
    }

    internal static ICommandResult CreateResult(PaletteItemView item, SystemInvokerKind kind)
    {
        var action = kind switch
        {
            SystemInvokerKind.PickFiles => Resources.UnsupportedFilePicker,
            SystemInvokerKind.PickFolders => Resources.UnsupportedFolderPicker,
            SystemInvokerKind.PickAgent => Resources.UnsupportedAgentPicker,
            _ => string.Format(CultureInfo.CurrentCulture, Resources.UnsupportedSystemAction, kind),
        };
        WupDiagnostics.Write($"Unsupported system invoker {kind} for '{item.ExtensionId}:{item.Id}'.");
        return CommandResult.ShowToast(new ToastArgs
        {
            Message = string.Format(CultureInfo.CurrentCulture, Resources.UnsupportedActionMessage, item.Title, action),
            Result = CommandResult.KeepOpen(),
        });
    }
}
