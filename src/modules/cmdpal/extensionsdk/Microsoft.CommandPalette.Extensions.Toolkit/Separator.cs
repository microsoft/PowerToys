// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation.Collections;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class Separator : BaseObservable, IListItem, ISeparatorContextItem, ISeparatorFilterItem, IExtendedAttributesProvider
{
    private readonly PropertySet _extendedAttributes = new();
    private ICommand? _sectionCommand;

    public IDetails? Details => null;

    public string? Section { get; private set; }

    public ITag[]? Tags => null;

    public string? TextToSuggest => null;

    public ICommand? Command => null;

    /// <summary>
    /// Gets or sets the optional command displayed alongside this separator when it is used as a section header.
    /// </summary>
    /// <remarks>
    /// <see cref="Command"/> remains <see langword="null"/> so hosts continue to identify this object as a separator.
    /// The section command is exposed to the host through <see cref="IExtendedAttributesProvider"/>.
    /// </remarks>
    public ICommand? SectionCommand
    {
        get => _sectionCommand;
        set
        {
            if (EqualityComparer<ICommand?>.Default.Equals(value, _sectionCommand))
            {
                return;
            }

            _sectionCommand = value;
            if (value is null)
            {
                _extendedAttributes.Remove(WellKnownExtensionAttributes.SectionCommand);
            }
            else
            {
                _extendedAttributes[WellKnownExtensionAttributes.SectionCommand] = value;
            }

            OnPropertyChanged();
        }
    }

    public IIconInfo? Icon => null;

    public IContextItem[]? MoreCommands => null;

    public string? Subtitle => null;

    public string? Title
    {
        get => Section;
        set
        {
            if (Section != value)
            {
                Section = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Section));
            }
        }
    }

    public Separator(string? title = "")
    {
        Section = title ?? string.Empty;
    }

    public Separator(string? title, ICommand? sectionCommand)
        : this(title)
    {
        SectionCommand = sectionCommand;
    }

    public IDictionary<string, object> GetProperties() => _extendedAttributes;
}
