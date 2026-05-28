// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerDisplay.Cli.Errors;

namespace PowerDisplay.Cli.Resolution;

/// <summary>
/// Validates 0-100 percentage values used for the continuous settings (brightness,
/// contrast, volume). Returns a structured <see cref="CliError"/> on failure so the
/// caller can surface a specific reason and the accepted range.
/// </summary>
public static class ContinuousValueValidator
{
    public const int Min = 0;
    public const int Max = 100;

    public static CliError? Validate(string settingName, int value)
    {
        if (value < Min || value > Max)
        {
            return new CliError
            {
                Code = CliErrorCodes.OutOfRange,
                ExitCode = CliExitCodes.OutOfRange,
                Setting = settingName,
                Requested = value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ExpectedRange = $"[{Min}, {Max}]",
                Message = $"--{settingName} value {value} is out of range; expected integer in [{Min}, {Max}]",
            };
        }

        return null;
    }
}
