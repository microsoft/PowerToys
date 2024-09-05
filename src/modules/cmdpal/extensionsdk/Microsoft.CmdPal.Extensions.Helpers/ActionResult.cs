namespace Microsoft.CmdPal.Extensions.Helpers;

public class ActionResult : ICommandResult
{
    private ICommandResultArgs _Args = null;
    private CommandResultKind _Kind = CommandResultKind.Dismiss;
    public ICommandResultArgs Args => _Args;
    public CommandResultKind Kind => _Kind;
    public static ActionResult Dismiss() {
        return new ActionResult() { _Kind = CommandResultKind.Dismiss };
    }
    public static ActionResult GoHome()
    {
        return new ActionResult() { _Kind = CommandResultKind.GoHome };
    }
    public static ActionResult KeepOpen()
    {
        return new ActionResult() { _Kind = CommandResultKind.KeepOpen };
    }
}
