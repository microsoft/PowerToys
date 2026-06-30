// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Resources;

namespace PowerDisplay.Cli.Properties;

/// <summary>
/// Strongly-typed accessor for the CLI's localizable human-readable strings (Resources.resx,
/// localized into satellite assemblies by the build pipeline).
/// Only prose lives here — error messages/hints and text-mode labels. The machine contract (JSON
/// keys, error <c>code</c> strings, <c>status</c> strings, exit codes, VCP names) stays as invariant
/// literals elsewhere and is never routed through this class.
/// </summary>
internal static class Resources
{
    private static readonly ResourceManager Manager =
        new("PowerDisplay.Cli.Properties.Resources", typeof(Resources).Assembly);

    // ---- plain (no-argument) labels ----
    internal static string Text_NoMonitorsDiscovered => Get(nameof(Text_NoMonitorsDiscovered));

    internal static string Text_NotSupported => Get(nameof(Text_NotSupported));

    internal static string Text_Unknown => Get(nameof(Text_Unknown));

    internal static string Text_Failed => Get(nameof(Text_Failed));

    internal static string Text_NotConnectedSkipped => Get(nameof(Text_NotConnectedSkipped));

    internal static string Text_NoSettingsInProfile => Get(nameof(Text_NoSettingsInProfile));

    internal static string Text_OutOfRangeSkipped => Get(nameof(Text_OutOfRangeSkipped));

    internal static string Text_NoProfilesSaved => Get(nameof(Text_NoProfilesSaved));

    internal static string Text_NoVcpCapabilities => Get(nameof(Text_NoVcpCapabilities));

    internal static string Text_NoValuesReported => Get(nameof(Text_NoValuesReported));

    // ---- error messages / hints (with arguments) ----
    internal static string Text_AppliedProfile(string profile) => Format(nameof(Text_AppliedProfile), profile);

    internal static string Warn_MonitorNumberIgnored(int number) => Format(nameof(Warn_MonitorNumberIgnored), number);

    internal static string Error_NoSettingSpecified => Get(nameof(Error_NoSettingSpecified));

    internal static string Error_OnlyOneSetting => Get(nameof(Error_OnlyOneSetting));

    internal static string Hint_OnlyOneSetting => Get(nameof(Hint_OnlyOneSetting));

    internal static string Error_UnknownSetting(string setting) => Format(nameof(Error_UnknownSetting), setting);

    internal static string Hint_ValidSettings(string settings) => Format(nameof(Hint_ValidSettings), settings);

    internal static string Error_TimedOut(int seconds) => Format(nameof(Error_TimedOut), seconds);

    internal static string Error_Cancelled => Get(nameof(Error_Cancelled));

    internal static string Error_InvalidArguments => Get(nameof(Error_InvalidArguments));

    internal static string Error_UnexpectedError(string message) => Format(nameof(Error_UnexpectedError), message);

    internal static string Error_ProviderUnavailable => Get(nameof(Error_ProviderUnavailable));

    internal static string Error_DeserializeMismatch => Get(nameof(Error_DeserializeMismatch));

    internal static string Error_NegativeStep => Get(nameof(Error_NegativeStep));

    internal static string Error_NoAdjustSettingSpecified => Get(nameof(Error_NoAdjustSettingSpecified));

    private static string Get(string name) => Manager.GetString(name, CultureInfo.CurrentUICulture) ?? name;

    // Defensive formatting: a translator can break a placeholder ({0} -> {1}, an unescaped brace,
    // an extra index). That must never crash the CLI or mask the real result. Try the localized
    // template; on FormatException fall back to the neutral (English) template we ship and control;
    // if even that is malformed, return it unformatted. So a broken translation degrades to English.
    private static string Format(string name, params object[] args)
    {
        var localized = Manager.GetString(name, CultureInfo.CurrentUICulture);
        if (localized is not null)
        {
            try
            {
                return string.Format(CultureInfo.CurrentCulture, localized, args);
            }
            catch (FormatException)
            {
            }
        }

        var neutral = Manager.GetString(name, CultureInfo.InvariantCulture) ?? name;
        return SafeFormat(neutral, args);
    }

    // Formats with the invariant English template, swallowing a malformed-template FormatException
    // by returning the template unformatted. Internal so the no-crash guarantee can be unit-tested.
    internal static string SafeFormat(string template, params object[] args)
    {
        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}
