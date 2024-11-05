
using System.Diagnostics;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Extensions.Helpers;

public sealed partial class OpenUrlCommand : InvokableCommand
{
    private readonly string _target;
    public CommandResult Result { get; set; } = CommandResult.KeepOpen();

    public OpenUrlCommand(string target)
    {
        _target = target;
        Name = "Open";
        Icon = new("\uE8A7");
    }

    public override CommandResult Invoke()
    {
        Process.Start(new ProcessStartInfo(_target) { UseShellExecute = true });
        return Result;
    }
}
