namespace Microsoft.Windows.CommandPalette.Extensions.Helpers;

public class NoOpAction : InvokableCommand
{
    public override ICommandResult Invoke() => ActionResult.KeepOpen();
}
