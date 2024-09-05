namespace Microsoft.CmdPal.Extensions.Helpers;

public class Details : BaseObservable, IDetails
{
    private IconDataType _heroImage = new(string.Empty);
    private string _title = string.Empty;
    private string _body = string.Empty;
    private IDetailsElement[] _metadata = [];

    public IconDataType HeroImage
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
