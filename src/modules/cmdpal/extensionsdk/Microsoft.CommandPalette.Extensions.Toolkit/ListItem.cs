// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Collections;
using WinRT;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ListItem : CommandItem, IListItem, IExtendedAttributesProvider
{
    private static readonly PropertySet EmptyExtendedAttributes = new();

    private ITag[] _tags = [];
    private IDetails? _details;

    private string _section = string.Empty;
    private string _textToSuggest = string.Empty;

    private PropertySet? _extendedAttributes;
    private DataPackage? _dataPackage;
    private DataPackageView? _dataPackageView;

    public virtual ITag[] Tags
    {
        get => _tags;
        set
        {
            _tags = value;
            OnPropertyChanged(nameof(Tags));
        }
    }

    public virtual IDetails? Details
    {
        get => _details;
        set
        {
            _details = value;
            OnPropertyChanged(nameof(Details));
        }
    }

    public virtual string Section
    {
        get => _section;
        set
        {
            _section = value;
            OnPropertyChanged(nameof(Section));
        }
    }

    public virtual string TextToSuggest
    {
        get => _textToSuggest;
        set
        {
            _textToSuggest = value;
            OnPropertyChanged(nameof(TextToSuggest));
        }
    }

    public DataPackage? DataPackage
    {
        get => _dataPackage;
        set
        {
            _dataPackage = value;
            _dataPackageView = null;
            _extendedAttributes ??= new PropertySet();
            _extendedAttributes[WellKnownExtensionAttributes.DataPackage] = value?.AsAgile().Get()?.GetView()!;
        }
    }

    public DataPackageView? DataPackageView
    {
        get => _dataPackageView;
        set
        {
            _dataPackage = null;
            _dataPackageView = value;
            _extendedAttributes ??= new PropertySet();
            _extendedAttributes[WellKnownExtensionAttributes.DataPackage] = value?.AsAgile().Get()!;
        }
    }

    public ListItem(ICommand command)
        : base(command)
    {
    }

    public ListItem(ICommandItem command)
        : base(command)
    {
    }

    public ListItem()
        : base()
    {
    }

    public IDictionary<string, object> GetProperties()
    {
        return _extendedAttributes ?? new PropertySet();
    }
}
