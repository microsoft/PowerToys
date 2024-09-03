namespace Microsoft.Windows.CommandPalette.Extensions.Helpers;

public class Action : BaseObservable, ICommand
{
    protected string _Name = "";
    protected IconDataType _Icon = new("");
    public string Name { get => _Name; set { _Name = value; OnPropertyChanged(nameof(Name)); } }
    public IconDataType Icon { get => _Icon; set { _Icon = value; OnPropertyChanged(nameof(Icon)); } }
}
