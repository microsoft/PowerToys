// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Defines a fallback that the host formats without sending each query to the extension.
/// </summary>
public partial class FallbackPassiveItem : FallbackCommandItem, IFallbackCommandItem3
{
    private string? _name;
    private string _titleTemplate = string.Empty;
    private string _subtitleTemplate = string.Empty;
    private HostMatchKind _matchKind;
    private string _matchValue = string.Empty;

    public FallbackPassiveItem(string displayTitle, string id)
        : base(displayTitle, id)
    {
    }

    public FallbackPassiveItem(ICommand command, string displayTitle, string id)
        : base(command, displayTitle, id)
    {
        FallbackHandler = this;
    }

    /// <summary>
    /// Gets or sets the action label shown before CreateCommand runs. Until explicitly
    /// set, the label follows the current command's name.
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

    public IFallbackHandler2? QueryHandler => null;

    IFallbackHandler2 IFallbackCommandItem3.QueryHandler => QueryHandler!;

    public override void UpdateQuery(string query)
    {
    }

    public virtual ICommand CreateCommand(IFallbackCommandInvocationArgs args) => Command!;

    private protected override void OnCommandChanged(string? propertyName)
    {
        if (_name is null && (propertyName is null || propertyName == nameof(ICommand.Name)))
        {
            OnPropertyChanged(nameof(Name));
        }
    }
}
