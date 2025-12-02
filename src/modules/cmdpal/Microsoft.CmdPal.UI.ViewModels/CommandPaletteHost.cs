// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CommandPalette.Extensions;

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
        return Extension?.ExtensionDisplayName ?? _builtInProvider?.DisplayName ?? _builtInProvider?.Id;
    }
}
