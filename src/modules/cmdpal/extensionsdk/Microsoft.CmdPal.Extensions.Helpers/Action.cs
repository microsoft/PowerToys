namespace Microsoft.CmdPal.Extensions.Helpers;

public class Action : BaseObservable, ICommand
{
    private string _name = string.Empty;
    private IconDataType _icon = new(string.Empty);

    public string Name
    {
        get => _name;
        set
        {
            _name = value; 
            OnPropertyChanged(nameof(Name));
        }
    }

    public IconDataType Icon
    {
        get => _icon;
        set
        {
            _icon = value; 
            OnPropertyChanged(nameof(Icon));
        }
    }
}
