// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// A sound choice offered for a cue: a built-in id, the custom sentinel, or null for "no sound".
/// </summary>
public sealed record AudioCueSoundOption(string? SoundId, string DisplayName);
