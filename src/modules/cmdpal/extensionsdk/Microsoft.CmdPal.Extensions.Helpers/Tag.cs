using Windows.UI;

namespace Microsoft.CmdPal.Extensions.Helpers;

public class Tag : BaseObservable, ITag
{
    protected Color _Color = new();
    protected IconDataType _Icon = null;
    protected string _Text = "";
    protected string _ToolTip = "";
    protected ICommand _Action;

    public Color Color { get => _Color; set { _Color = value; OnPropertyChanged(nameof(Color)); } }
    public IconDataType Icon { get => _Icon; set { _Icon = value; OnPropertyChanged(nameof(Icon)); } }
    public string Text { get => _Text; set { _Text = value; OnPropertyChanged(nameof(Text)); } }
    public string ToolTip { get => _ToolTip; set { _ToolTip = value; OnPropertyChanged(nameof(ToolTip)); } }
    public ICommand Command { get => _Action; set { _Action = value; OnPropertyChanged(nameof(Action)); } }

}
