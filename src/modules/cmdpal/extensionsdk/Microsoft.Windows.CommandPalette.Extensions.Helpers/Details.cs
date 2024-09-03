namespace Microsoft.Windows.CommandPalette.Extensions.Helpers;

public class Details : BaseObservable, IDetails
{
    protected IconDataType _HeroImage;
    protected string _Title;
    protected string _Body;
    protected IDetailsElement[] _Metadata = [];

    public IconDataType HeroImage { get => _HeroImage; set { _HeroImage = value; OnPropertyChanged(nameof(HeroImage)); } }
    public string Title { get => _Title; set { _Title = value; OnPropertyChanged(nameof(Title)); } }
    public string Body { get => _Body; set { _Body = value; OnPropertyChanged(nameof(Body)); } }
    public IDetailsElement[] Metadata { get => _Metadata; set { _Metadata = value; OnPropertyChanged(nameof(Metadata)); } }
}
