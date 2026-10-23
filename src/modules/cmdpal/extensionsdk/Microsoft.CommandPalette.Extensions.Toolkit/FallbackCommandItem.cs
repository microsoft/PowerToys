// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Defines a fallback command. By default this is an <see cref="FallbackCommandMode.Active"/>
/// fallback, which receives <see cref="UpdateQuery(string)"/> for each query.
/// Use <see cref="PassiveFallbackCommandItem"/> or <see cref="FallbackResultSource"/>
/// for the other fallback modes.
/// </summary>
public partial class FallbackCommandItem : CommandItem, IFallbackCommandItem, IFallbackHandler, IFallbackCommandItem2, IFallbackCommandItem3, IExtendedAttributesProvider
{
    private readonly IFallbackHandler? _fallbackHandler;
    private string? _name;
    private string _titleTemplate = string.Empty;
    private string _subtitleTemplate = string.Empty;
    private HostMatchKind _matchKind;
    private string _matchValue = string.Empty;
    private uint? _suggestedQueryDelayMilliseconds;
    private uint? _suggestedMinQueryLength;

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

    public virtual FallbackCommandMode Mode => FallbackCommandMode.Active;

    /// <summary>
    /// Gets or sets the name of the default action, which the host shows before it
    /// calls <see cref="CreateCommand(IFallbackCommandInvocationArgs)"/>. Until it
    /// is set, this is the name of the current <see cref="CommandItem.Command"/>.
    /// Setting it also sets the name of the current command, when that command is
    /// a toolkit <see cref="Toolkit.Command"/>.
    /// </summary>
    public virtual string Name
    {
        get => _name ?? Command?.Name ?? string.Empty;
        set
        {
            var oldName = Name;
            _name = value;
            if (Command is Command command)
            {
                command.Name = value;
            }

            if (oldName != Name)
            {
                OnPropertyChanged();
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

    /// <summary>
    /// Creates the command to run when the user activates this fallback. By default,
    /// this returns the current <see cref="CommandItem.Command"/>.
    /// </summary>
    public virtual ICommand CreateCommand(IFallbackCommandInvocationArgs args) => Command!;

    private protected override void OnCommandChanged(string? propertyName)
    {
        // Name follows the command's name until it is set explicitly.
        if (_name is null && (propertyName is null || propertyName == nameof(ICommand.Name)))
        {
            OnPropertyChanged(nameof(Name));
        }
    }
}
