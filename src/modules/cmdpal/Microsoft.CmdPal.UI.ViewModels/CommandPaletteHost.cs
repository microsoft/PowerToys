// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class CommandPaletteHost : AppExtensionHost, IExtensionHost
{
    // Static singleton, so that we can access this from anywhere
    // Post MVVM - this should probably be like, a dependency injection thing.
    public static CommandPaletteHost Instance { get; } = new();

    public IExtensionWrapper? Extension { get; }

    private readonly ICommandProvider? _builtInProvider;

    private CommandPaletteHost()
    {
    }

    public CommandPaletteHost(IExtensionWrapper source)
    {
        Extension = source;
    }

    public CommandPaletteHost(ICommandProvider builtInProvider)
    {
        _builtInProvider = builtInProvider;
    }

    public override string? GetExtensionDisplayName()
    {
        return Extension?.ExtensionDisplayName;
    }

    public override IAsyncAction ShowStatus(IStatusMessage? message, StatusContext context)
    {
        return Extension == null && this != Instance ? Instance.ShowStatus(message, context) : base.ShowStatus(message, context);
    }

    public override IAsyncAction HideStatus(IStatusMessage? message)
    {
        return Extension == null && this != Instance ? Instance.HideStatus(message) : base.HideStatus(message);
    }
}
