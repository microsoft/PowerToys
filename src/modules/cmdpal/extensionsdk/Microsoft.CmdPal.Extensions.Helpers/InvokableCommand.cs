namespace Microsoft.CmdPal.Extensions.Helpers;

public class InvokableCommand : Action, IInvokableCommand
{
    public virtual ICommandResult Invoke() => throw new NotImplementedException();
}
