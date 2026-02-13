// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Windows.Foundation.Collections;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class Details : BaseObservable, IDetails, IExtendedAttributesProvider
{
    public virtual IIconInfo HeroImage { get; set => SetProperty(ref field, value); } = new IconInfo();

    public virtual string Title { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual string Body { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual IDetailsElement[] Metadata { get; set => SetProperty(ref field, value); } = [];

    public virtual ContentSize Size { get; set => SetProperty(ref field, value); } = ContentSize.Small;

    public IDictionary<string, object>? GetProperties() => new ValueSet()
    {
        { "Size", (int)Size },
    };
}
