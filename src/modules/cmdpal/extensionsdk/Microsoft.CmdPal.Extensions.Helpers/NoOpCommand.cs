namespace Microsoft.CmdPal.Extensions.Helpers;

public class NoOpCommand : InvokableCommand
{
    public override ICommandResult Invoke() => CommandResult.KeepOpen();
}
