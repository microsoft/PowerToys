// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class FallbackCommandItem : CommandItem, IFallbackCommandItem, IFallbackHandler, IFallbackCommandItem2
{
    private readonly IFallbackHandler? _fallbackHandler;

    public FallbackCommandItem(string displayTitle, string id)
    {
        DisplayTitle = displayTitle;
        Id = id;
    }

    public FallbackCommandItem(ICommand command, string displayTitle, string id)
        : base(command)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A non-empty or whitespace Id must be provided.", nameof(id));
        }

        Id = id;
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

    public virtual string Id { get; }

    public virtual string DisplayTitle { get; }

    public virtual void UpdateQuery(string query) => _fallbackHandler?.UpdateQuery(query);
}
