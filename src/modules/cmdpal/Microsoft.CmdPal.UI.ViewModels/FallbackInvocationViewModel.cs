// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed partial class FallbackInvocationViewModel : CommandViewModel
{
    private readonly TopLevelFallbackViewModel _fallback;
    private readonly IFallbackCommandInvocationArgs? _args;

    internal FallbackQueryContext QueryContext { get; }

    public override bool IsSet => true;

    internal FallbackInvocationViewModel(
        TopLevelFallbackViewModel fallback,
        TopLevelViewModel source,
        string id,
        string name,
        IFallbackCommandInvocationArgs? args,
        CancellationToken queryToken)
        : base(null, source.ItemViewModel.PageContext)
    {
        _fallback = fallback;
        _args = args;
        QueryContext = new(source.ExtensionHost, source.ProviderContext, source, queryToken);
        Id = $"{id}.placeholder.{args?.QueryId}";
        Name = name;
        Icon = source.IconViewModel;
    }

    public override void FastInitializeProperties()
    {
    }

    public override void InitializeProperties()
    {
    }

    internal void UpdateName(string name)
    {
        Name = name;
        UpdateProperty(nameof(Name));
    }

    internal CommandViewModel? CreateCommand()
    {
        if (_args is null || !QueryContext.CanInvoke || IsCleanedUp || _fallback.IsCleanedUp)
        {
            return null;
        }

        var command = _fallback.CreateCommand(_args);
        if (command is null)
        {
            return null;
        }

        var viewModel = new CommandViewModel(command, PageContext);
        viewModel.FastInitializeProperties();
        return QueryContext.CanInvoke && !_fallback.IsCleanedUp ? viewModel : null;
    }
}
