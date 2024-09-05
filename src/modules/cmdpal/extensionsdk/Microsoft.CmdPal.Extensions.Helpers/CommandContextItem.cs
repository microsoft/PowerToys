namespace Microsoft.CmdPal.Extensions.Helpers;

public class CommandContextItem : ICommandContextItem
{
    public bool IsCritical { get; set; }
    public ICommand Command { get; set; }
    public string Tooltip { get; set; } = "";
    public CommandContextItem(ICommand command)
    {
        Command = command;
    }
}
