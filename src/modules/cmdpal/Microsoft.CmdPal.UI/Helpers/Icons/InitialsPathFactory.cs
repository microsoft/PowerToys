// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Produces normalized SVG path data for an initials string.
/// </summary>
internal delegate bool InitialsPathFactory(
    string text,
    out string pathData,
    out bool useEvenOddFill);
