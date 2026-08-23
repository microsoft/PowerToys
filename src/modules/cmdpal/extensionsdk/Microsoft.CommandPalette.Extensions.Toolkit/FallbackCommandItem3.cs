// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Defines a fallback command that uses the fallback v2 contract.
/// </summary>
public partial class FallbackCommandItem3 : FallbackCommandItem, IFallbackCommandItem3
{
    private string _name = string.Empty;
    private string _titleTemplate = string.Empty;
    private string _subtitleTemplate = string.Empty;
    private HostMatchKind _matchKind;
    private string _matchValue = string.Empty;
    private uint? _suggestedQueryDelayMilliseconds;
    private uint? _suggestedMinQueryLength;

    public FallbackCommandItem3(string displayTitle, string id)
        : base(displayTitle, id)
    {
        Name = displayTitle;
    }

    public FallbackCommandItem3(ICommand command, string displayTitle, string id)
        : base(command, displayTitle, id)
    {
        Name = command.Name;
    }

    public virtual FallbackCommandMode Mode => FallbackCommandMode.Active;

    public virtual string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value) && Command is Command command)
            {
                command.Name = value;
            }
        }
    }

    public virtual string TitleTemplate
    {
        get => _titleTemplate;
        set => SetProperty(ref _titleTemplate, value);
    }

    public virtual string SubtitleTemplate
    {
        get => _subtitleTemplate;
        set => SetProperty(ref _subtitleTemplate, value);
    }

    public virtual HostMatchKind MatchKind
    {
        get => _matchKind;
        set => SetProperty(ref _matchKind, value);
    }

    public virtual string MatchValue
    {
        get => _matchValue;
        set => SetProperty(ref _matchValue, value);
    }

    public virtual uint? SuggestedQueryDelayMilliseconds
    {
        get => _suggestedQueryDelayMilliseconds;
        set => SetProperty(ref _suggestedQueryDelayMilliseconds, value);
    }

    public virtual uint? SuggestedMinQueryLength
    {
        get => _suggestedMinQueryLength;
        set => SetProperty(ref _suggestedMinQueryLength, value);
    }

    public virtual IFallbackHandler2? QueryHandler { get; init; }

    OptionalUInt32 IFallbackCommandItem3.SuggestedQueryDelayMilliseconds => SuggestedQueryDelayMilliseconds.ToOptionalUInt32();

    OptionalUInt32 IFallbackCommandItem3.SuggestedMinQueryLength => SuggestedMinQueryLength.ToOptionalUInt32();

    IFallbackHandler2 IFallbackCommandItem3.QueryHandler => QueryHandler!;

    public virtual ICommand CreateCommand(IFallbackCommandInvocationArgs args) => Command!;
}
