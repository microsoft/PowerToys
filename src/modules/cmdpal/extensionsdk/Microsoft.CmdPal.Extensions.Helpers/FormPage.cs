namespace Microsoft.CmdPal.Extensions.Helpers;

public class FormPage : Action, IFormPage
{
    private bool _Loading = false;

    public bool Loading { get => _Loading; set { _Loading = value; OnPropertyChanged(nameof(Loading)); } }

    public virtual IForm[] Forms() => throw new NotImplementedException();
}
