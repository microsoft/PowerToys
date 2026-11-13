// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using PowerScripts.Client;

using var input = Console.OpenStandardInput();
using var output = Console.OpenStandardOutput();
while (await PowerScriptsSessionProtocol.ReadAsync(input) is { } frame)
{
    using var document = JsonDocument.Parse(frame);
    var command = document.RootElement.GetProperty("arguments").EnumerateArray().Select(value => value.GetString()!).ToArray();
    if (command[0] is "block" or "exit")
    {
        await File.AppendAllTextAsync(command[1], Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
        if (command[0] == "exit")
        {
            return 7;
        }

        while (!File.Exists(command[2]))
        {
            await Task.Delay(10);
        }
    }

    await PowerScriptsSessionProtocol.WriteAsync(
        output,
        PowerScriptsSessionProtocol.CreateResponse(
            0,
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
            document.RootElement.GetProperty("input").GetString() ?? string.Empty));
}

return 0;
