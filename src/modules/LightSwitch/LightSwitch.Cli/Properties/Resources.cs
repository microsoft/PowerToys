// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Resources;

namespace LightSwitch.Cli.Properties;

// Only human-readable output uses these resources. Protocol values remain invariant.
internal static class Resources
{
    private static readonly ResourceManager Manager = new("LightSwitch.Cli.Properties.Resources", typeof(Resources).Assembly);

    internal static string Help_Text => Get(nameof(Help_Text)).ReplaceLineEndings();

    internal static string Description_Root => Get(nameof(Description_Root));

    internal static string Description_Schedule => Get(nameof(Description_Schedule));

    internal static string Description_Enable => Get(nameof(Description_Enable));

    internal static string Description_Disable => Get(nameof(Description_Disable));

    internal static string Description_Mode => Get(nameof(Description_Mode));

    internal static string Description_Status => Get(nameof(Description_Status));

    internal static string Description_Light => Get(nameof(Description_Light));

    internal static string Description_Dark => Get(nameof(Description_Dark));

    internal static string Description_Toggle => Get(nameof(Description_Toggle));

    internal static string Description_Json => Get(nameof(Description_Json));

    internal static string Description_Help => Get(nameof(Description_Help));

    internal static string Description_Version => Get(nameof(Description_Version));

    internal static string Error_UnknownScheduleMode => Get(nameof(Error_UnknownScheduleMode));

    internal static string Error_CommandRequired => Get(nameof(Error_CommandRequired));

    internal static string Error_TimedOut => Get(nameof(Error_TimedOut));

    internal static string Error_UnexpectedFailure => Get(nameof(Error_UnexpectedFailure));

    internal static string Error_RequestTooLarge => Get(nameof(Error_RequestTooLarge));

    internal static string Error_UntrustedServer => Get(nameof(Error_UntrustedServer));

    internal static string Error_ServiceUnavailable => Get(nameof(Error_ServiceUnavailable));

    internal static string Error_ConnectionClosed => Get(nameof(Error_ConnectionClosed));

    internal static string Error_InvalidUtf16 => Get(nameof(Error_InvalidUtf16));

    internal static string Error_ResponseTooLarge => Get(nameof(Error_ResponseTooLarge));

    internal static string Error_InvalidResponse => Get(nameof(Error_InvalidResponse));

    internal static string Hint_Usage => Get(nameof(Hint_Usage));

    internal static string Text_SystemTheme(string value) => Format(nameof(Text_SystemTheme), value);

    internal static string Text_AppsTheme(string value) => Format(nameof(Text_AppsTheme), value);

    internal static string Text_ChangeSystem(string value) => Format(nameof(Text_ChangeSystem), value);

    internal static string Text_ChangeApps(string value) => Format(nameof(Text_ChangeApps), value);

    internal static string Text_ScheduleMode(string value) => Format(nameof(Text_ScheduleMode), value);

    internal static string Text_ManualOverride(string value) => Format(nameof(Text_ManualOverride), value);

    internal static string Value_Light => Get(nameof(Value_Light));

    internal static string Value_Dark => Get(nameof(Value_Dark));

    internal static string Value_Unknown => Get(nameof(Value_Unknown));

    internal static string Value_Enabled => Get(nameof(Value_Enabled));

    internal static string Value_Disabled => Get(nameof(Value_Disabled));

    internal static string Value_Active => Get(nameof(Value_Active));

    internal static string Value_Inactive => Get(nameof(Value_Inactive));

    internal static string Value_Off => Get(nameof(Value_Off));

    internal static string Value_FixedHours => Get(nameof(Value_FixedHours));

    internal static string Value_SunsetToSunrise => Get(nameof(Value_SunsetToSunrise));

    internal static string Value_FollowNightLight => Get(nameof(Value_FollowNightLight));

    private static string Get(string name) => Manager.GetString(name, CultureInfo.CurrentUICulture) ?? name;

    private static string Format(string name, string value) => string.Format(CultureInfo.CurrentCulture, Get(name), value);
}
