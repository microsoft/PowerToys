namespace Microsoft.Windows.CommandPalette.Extensions.Helpers;

public class Action : BaseObservable, ICommand
{
    private string _name = "";
    private IconDataType _icon = new("");

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
