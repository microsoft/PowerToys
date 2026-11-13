// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace RobocopyUI.Models;

/// <summary>
/// Simple-mode job, toggles, and whether Advanced extras are still on the command line.
/// </summary>
/// <param name="Kind">The radio job inferred from the switches.</param>
/// <param name="Options">The Simple toggles inferred from the switches.</param>
/// <param name="HasAdditionalOptions">True when switches Simple cannot represent are present.</param>
public sealed record SimpleCopySnapshot(SimpleCopyTaskKind Kind, SimpleCopyOptions Options, bool HasAdditionalOptions);
