// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Pipes;
using System.Text;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Commands;

internal sealed partial class ZoomItActionCommand : InvokableCommand
{
    private readonly string _action;
    private readonly string _title;

    private const string PipeName = "powertoys_zoomit_cmd";

    public ZoomItActionCommand(string action, string title)
    {
        _action = action;
        _title = title;
        Name = title;
    }

    public override CommandResult Invoke()
    {
        try
        {
            var payload = $"{{\"action\":\"{_action}\"}}";
            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);

            client.Connect(200);
            var bytes = Encoding.UTF8.GetBytes(payload);
            client.Write(bytes, 0, bytes.Length);
            client.Flush();

            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to invoke ZoomIt ({_title}): {ex.Message}");
        }
    }
}
