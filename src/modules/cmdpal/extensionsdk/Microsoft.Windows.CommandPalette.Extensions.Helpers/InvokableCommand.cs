namespace Microsoft.Windows.CommandPalette.Extensions.Helpers;

public class InvokableCommand : Action, IInvokableCommand
{
    public virtual ICommandResult Invoke() => throw new NotImplementedException();
}
