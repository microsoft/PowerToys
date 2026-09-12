// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace LightSwitch.Cli.Protocol;

internal static class CliProtocol
{
    internal const int Version = 1;
    internal const int MaxMessageChars = 32 * 1024;
    internal const int BufferSize = 1024;

    // C++ wchar_t payloads and this encoding agree on UTF-16 LE. Do not emit or detect a BOM.
    internal static readonly Encoding Encoding = new UnicodeEncoding(false, false, true);

    internal static string PipeName
    {
        get
        {
            using var process = Process.GetCurrentProcess();
            return $"PowerToys_LightSwitch_Cli_{process.SessionId}";
        }
    }

    internal static CliResponse ParseResponse(string json)
    {
        if (json.Length > MaxMessageChars)
        {
            throw InvalidResponse();
        }

        CliResponse? response;
        try
        {
            response = JsonSerializer.Deserialize(json, CliJsonContext.Default.CliResponse);
        }
        catch (JsonException)
        {
            throw InvalidResponse();
        }

        if (response is null || response.Version != Version ||
            (response.Success && (response.State is null || response.Error is not null)) ||
            (!response.Success && (response.Error is null || string.IsNullOrWhiteSpace(response.Error.Code) || string.IsNullOrWhiteSpace(response.Error.Message))))
        {
            throw InvalidResponse();
        }

        if (response.State is not null &&
            (response.State.SystemTheme is not ("light" or "dark" or "unknown") ||
             response.State.AppsTheme is not ("light" or "dark" or "unknown") ||
             response.State.ScheduleMode is not ("Off" or "FixedHours" or "SunsetToSunrise" or "FollowNightLight")))
        {
            throw InvalidResponse();
        }

        return response;
    }

    internal static int ExitCode(string code) => code switch
    {
        "INVALID_ARGUMENT" or "INVALID_CONFIGURATION" => 2,
        "SERVICE_UNAVAILABLE" or "SERVER_BUSY" => 3,
        "TIMEOUT" => 4,
        _ => 1,
    };

    private static CliException InvalidResponse() => new("PROTOCOL_ERROR", "Light Switch returned an invalid or incompatible response.");
}
