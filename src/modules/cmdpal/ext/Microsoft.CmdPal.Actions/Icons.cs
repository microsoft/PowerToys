// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Actions;

internal sealed class Icons
{
    internal static IconInfo CopilotSvg { get; } = IconHelpers.FromRelativePath("Assets\\Microsoft_Copilot_Icon.svg");

    internal static IconInfo CopilotPng { get; } = IconHelpers.FromRelativePath("Assets\\Microsoft_Copilot_Icon.png");

    internal static IconInfo ActionsPng { get; } = IconHelpers.FromRelativePath("Assets\\Actions.png");

    // windows settings
    internal static IconInfo Settings { get; } = IconHelpers.FromRelativePath("Assets\\WindowsSettings.svg");

    // google's icon
    internal static IconInfo Google { get; } = new IconInfo("https://www.google.com/favicon.ico");

    // raycast's icon
    internal static IconInfo Raycast { get; } = IconHelpers.FromRelativePath("Assets\\Raycast_idE-hcBj9B_1.png");

    internal static IconInfo Pwsh { get; } = IconHelpers.FromRelativePath("Assets\\Powershell.svg");

    internal static IconInfo Python { get; } = IconHelpers.FromRelativePath("Assets\\Python.svg");

    internal static IconInfo Bash { get; } = IconHelpers.FromRelativePath("Assets\\Bash.svg");

    // google calendar's icon
    internal static IconInfo GoogleCalendar { get; } = new IconInfo("https://upload.wikimedia.org/wikipedia/commons/a/a5/Google_Calendar_icon_%282020%29.svg");

    // bing's icon
    internal static IconInfo Bing { get; } = new IconInfo("https://www.bing.com/sa/simg/favicon-2x.ico");

    // Action input icons
    internal static IconInfo DocumentInput { get; } = new IconInfo("Assets\\Document.png");

    internal static IconInfo FileInput { get; } = new IconInfo("\uE8A5");

    internal static IconInfo PhotoInput { get; } = new IconInfo("\uE91b");

    internal static IconInfo TextInput { get; } = new IconInfo("\uE710");

    internal static IconInfo StreamingTextInput { get; } = new IconInfo("\uE710");

    internal static IconInfo RemoteFileInput { get; } = new IconInfo("\uE8E5");

    internal static IconInfo TableInput { get; } = new IconInfo("\uf575");

    internal static IconInfo ContactInput { get; } = new IconInfo("\uE77b");
}
