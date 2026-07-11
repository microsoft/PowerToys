// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

/// <summary>
/// A Windows system sound offered as a cue option. Resolved at playback time from the user's
/// sound scheme (AppEvents\Schemes\Apps\{EventApp}\{EventName}) when set, falling back to the
/// stock file under %WINDIR%\Media. Nothing is redistributed; the files ship with Windows.
/// </summary>
public sealed record AudioCueSystemSound(string Id, string DisplayNameResourceKey, string? EventApp, string? EventName, string FallbackFileName);
