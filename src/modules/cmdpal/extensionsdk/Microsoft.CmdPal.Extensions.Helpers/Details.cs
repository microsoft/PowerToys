// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public partial class Details : BaseObservable, IDetails
{
    private IconInfo _heroImage = new(string.Empty);
    private string _title = string.Empty;
    private string _body = string.Empty;
    private IDetailsElement[] _metadata = [];

    public IconInfo HeroImage
    {
        get => _heroImage;
        set
        {
            _heroImage = value;
            OnPropertyChanged(nameof(HeroImage));
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    public string Body
    {
        get => _body;
        set
        {
            _body = value;
            OnPropertyChanged(nameof(Body));
        }
    }

    public IDetailsElement[] Metadata
    {
        get => _metadata;
        set
        {
            _metadata = value;
            OnPropertyChanged(nameof(Metadata));
        }
    }
}
