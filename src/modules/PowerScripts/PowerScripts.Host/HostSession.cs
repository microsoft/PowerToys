// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using PowerScripts.Client;

namespace PowerScripts.Host;

internal static class HostSession
{
    internal static int Run(Func<string[], int> invoke)
    {
        using var input = System.Console.OpenStandardInput();
        using var output = System.Console.OpenStandardOutput();
        while (PowerScriptsSessionProtocol.ReadAsync(input).GetAwaiter().GetResult() is byte[] request)
        {
            using var document = JsonDocument.Parse(request);
            var root = document.RootElement;
            var arguments = root.GetProperty("arguments").EnumerateArray()
                .Select(value => value.GetString() ?? throw new JsonException("Command arguments must be strings.")).ToArray();
            var payload = root.GetProperty("input");
            using var stdout = new StringWriter(CultureInfo.InvariantCulture);
            using var stderr = new StringWriter(CultureInfo.InvariantCulture);
            int exitCode;
            using (HostConsole.Capture(payload.ValueKind == JsonValueKind.Null ? string.Empty : payload.GetString()!, stdout, stderr))
            {
                // Sessions are non-interactive and cannot modify settings or grant trust.
                if (arguments.Length > 0 &&
                    (arguments[0] == "list" || (arguments[0] == "transform" && arguments.Contains("--no-consent", StringComparer.Ordinal))))
                {
                    exitCode = invoke(arguments);
                }
                else
                {
                    stderr.WriteLine("Host sessions accept list and transform --no-consent only.");
                    exitCode = PowerScriptsProtocol.ExitCode.Usage;
                }
            }

            PowerScriptsSessionProtocol.WriteAsync(
                output,
                PowerScriptsSessionProtocol.CreateResponse(exitCode, stdout.ToString(), stderr.ToString())).GetAwaiter().GetResult();
        }

        return 0;
    }
}
