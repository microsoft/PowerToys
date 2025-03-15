// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class FallbackCommandItem : CommandItem, IFallbackCommandItem, IFallbackHandler
{
    private IFallbackHandler? _fallbackHandler;

    public FallbackCommandItem(ICommand command, string displayTitle)
        : base(command)
    {
        DisplayTitle = displayTitle;
        if (command is IFallbackHandler f)
        {
            _fallbackHandler = f;
        }
    }

    public IFallbackHandler? FallbackHandler
    {
        get => _fallbackHandler ?? this;
        init => _fallbackHandler = value;
    }

    public virtual string DisplayTitle { get; }

    public virtual void UpdateQuery(string query) => _fallbackHandler?.UpdateQuery(query);
}
