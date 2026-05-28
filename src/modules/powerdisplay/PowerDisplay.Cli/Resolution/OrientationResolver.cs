// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using PowerDisplay.Cli.Errors;

namespace PowerDisplay.Cli.Resolution;

/// <summary>
/// Parses the user-facing degree value (<c>0</c>, <c>90</c>, <c>180</c>, <c>270</c>)
/// into the internal <see cref="DisplayRotationService"/> index (<c>0</c>, <c>1</c>,
/// <c>2</c>, <c>3</c>). Anything else returns a structured error that lists the
/// accepted values verbatim.
/// </summary>
public static class OrientationResolver
{
    public const string SettingName = "orientation";

    private static readonly string[] AcceptedDegrees = ["0", "90", "180", "270"];

    public static int? TryResolve(string raw, out CliError? error)
    {
        error = null;

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var degrees))
        {
            error = MakeError(raw);
            return null;
        }

        switch (degrees)
        {
            case 0: return 0;
            case 90: return 1;
            case 180: return 2;
            case 270: return 3;
            default:
                error = MakeError(raw);
                return null;
        }
    }

    private static CliError MakeError(string raw) => new()
    {
        Code = CliErrorCodes.InvalidDiscreteValue,
        ExitCode = CliExitCodes.InvalidDiscreteValue,
        Setting = SettingName,
        Requested = raw,
        Message = $"--orientation value '{raw}' is not accepted; expected one of {string.Join(", ", AcceptedDegrees)}",
        Hint = "pass the rotation in degrees (0, 90, 180, or 270)",
    };
}
