// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

/// <summary>
/// Describes a built-in sound set; per-cue files are named "{cue id}-{sound id}.wav".
/// </summary>
public sealed record AudioCueSoundDefinition(string Id, string DisplayNameResourceKey);
