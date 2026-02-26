// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class CommandPaletteHost : AppExtensionHost, IExtensionHost
{
    public IExtensionWrapper? Extension { get; }

    private readonly ICommandProvider? _builtInProvider;

    public CommandPaletteHost(ILogger<CommandPaletteHost> logger)
         : base(logger)
    {
    }

    public CommandPaletteHost(IExtensionWrapper source, ILogger<CommandPaletteHost> logger)
      : base(logger)
    {
        Extension = source;
    }

    public CommandPaletteHost(ICommandProvider builtInProvider, ILogger<CommandPaletteHost> logger)
        : base(logger)
    {
        _builtInProvider = builtInProvider;
    }

    public override string? GetExtensionDisplayName()
    {
        return Extension?.ExtensionDisplayName ?? _builtInProvider?.DisplayName ?? _builtInProvider?.Id;
    }
}
