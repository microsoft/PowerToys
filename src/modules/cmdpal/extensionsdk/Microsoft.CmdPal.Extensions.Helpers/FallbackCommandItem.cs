// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public partial class FallbackCommandItem : CommandItem, IFallbackCommandItem, IFallbackHandler
{
    private IFallbackHandler? _fallbackHandler;

    public FallbackCommandItem(ICommand command)
        : base(command)
    {
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

    public virtual void UpdateQuery(string query) => _fallbackHandler?.UpdateQuery(query);
}
